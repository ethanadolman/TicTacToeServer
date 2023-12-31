using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using UnityEditor.MemoryProfiler;
using UnityEngine;


static public class NetworkServerProcessing
{

    public static LinkedList<Credentials> UserDatabase;

    public static Dictionary<int, string> LoggedInUsers = new Dictionary<int, string>();

    public static LinkedList<GameRoom> GameRoomList = new LinkedList<GameRoom>();
    #region Send and Receive Data Functions
    static public void ReceivedMessageFromClient(string msg, int clientConnectionID, TransportPipeline pipeline)
    {
        Debug.Log("Network msg received =  " + msg + ", from connection id = " + clientConnectionID + ", from pipeline = " + pipeline);

        string[] csv = msg.Split(',');
        int signifier = int.Parse(csv[0]);


        switch (signifier)
        {
            case ClientToServerSignifiers.Login:
            {
                foreach (var User in UserDatabase)
                {
                    if (User.username == csv[1])
                    {
                        if (User.password == csv[2])
                        {
                            if(LoggedInUsers.ContainsValue(User.username)) 
                                SendMessageToClient($"{ServerToClientSignifiers.LoginFail},User Already Logged in", clientConnectionID, pipeline);
                            else
                            {
                                SendMessageToClient($"{ServerToClientSignifiers.LoginSuccess},login successful", clientConnectionID, pipeline);
                                LoggedInUsers.Add(clientConnectionID, User.username);
                            }

                            return;
                        }
                        SendMessageToClient($"{ServerToClientSignifiers.LoginFail},Incorrect Password", clientConnectionID, pipeline);
                        return;
                    }
                }
                SendMessageToClient($"{ServerToClientSignifiers.UserNotExist},user does not exist", clientConnectionID, pipeline);
                break;
            }
            case ClientToServerSignifiers.CreateAccount:
            {
                foreach (var User in UserDatabase)
                {
                    if (User.username == csv[1])
                    {
                    
                        SendMessageToClient($"{ServerToClientSignifiers.UsernameInUse},username already in use", clientConnectionID, pipeline);
                        return;
                    }
                    else if (csv[1].Contains(",") || csv[2].Contains(","))
                    {
                        SendMessageToClient($"{ServerToClientSignifiers.FormatError},username/password cannot contain commas ',' ", clientConnectionID, pipeline);
                    }
                }
                SendMessageToClient($"{ServerToClientSignifiers.AccountCreated},account created", clientConnectionID, pipeline);
                UserDatabase.AddLast(new Credentials(csv[1], csv[2]));
                break;
            }
            case ClientToServerSignifiers.FindGame:
            {
                foreach (var Room in GameRoomList)
                {
                    if (Room.name != csv[1]) continue;
                    if (Room.isFull && !Room.isInProgress)
                    {
                        Room.observerIDs.AddLast(clientConnectionID);
                        SendMessageToClient($"{ServerToClientSignifiers.FullGameRoomFound},{LoggedInUsers[Room.hostUserID]},{LoggedInUsers[Room.clientUserID]}, Joining full game", clientConnectionID, pipeline);
                            return;
                    }
                    else if (Room.isInProgress)
                    {
                        Room.observerIDs.AddLast(clientConnectionID);
                        string tiles = "";
                        foreach (var tile in Room.tiles)
                        {
                            if(tiles == "") tiles += tile;
                            else tiles += "," + tile;
                        }
                        SendMessageToClient($"{ServerToClientSignifiers.FullGameRoomFoundInProgress},{LoggedInUsers[Room.hostUserID]},{LoggedInUsers[Room.clientUserID]},{tiles}", clientConnectionID, pipeline);
                        return;
                    }
                    else
                    {
                        Room.clientUserID = clientConnectionID;
                        Room.isFull = true;
                            SendMessageToClient($"{ServerToClientSignifiers.GameRoomFound},{LoggedInUsers[Room.hostUserID]},{LoggedInUsers[Room.clientUserID]}, Existing game room found", clientConnectionID, pipeline);
                            SendMessageToClient($"{ServerToClientSignifiers.ClientJoined},{LoggedInUsers[Room.hostUserID]},{LoggedInUsers[Room.clientUserID]}, Client has joined", Room.hostUserID, pipeline);
                        return;
                    }
                }
                    SendMessageToClient($"{ServerToClientSignifiers.NewGameRoom},{LoggedInUsers[clientConnectionID]}, Creating new game room", clientConnectionID, pipeline);
                GameRoomList.AddLast(new GameRoom(csv[1], clientConnectionID));
                break;
            }
            case ClientToServerSignifiers.GameStart:
            {
                GameRoom Room = GameRoomList.FirstOrDefault(x => x.name == csv[1]);
                if (Room == null) return; //if this runs we have a serious problem.
                if (Room.isFull)
                {
                    SendMessageToClient($"{ServerToClientSignifiers.GameStartSuccess},{LoggedInUsers[Room.hostUserID]},{LoggedInUsers[Room.clientUserID]}, Starting Game", Room.hostUserID, pipeline);
                    SendMessageToClient($"{ServerToClientSignifiers.GameStartSuccess},{LoggedInUsers[Room.hostUserID]},{LoggedInUsers[Room.clientUserID]}, Starting Game", Room.clientUserID, pipeline);
                    foreach (var ID in Room.observerIDs) { SendMessageToClient($"{ServerToClientSignifiers.GameStartSuccess},{LoggedInUsers[Room.hostUserID]},{LoggedInUsers[Room.clientUserID]}, Starting Game", ID, pipeline); }
                    Room.isInProgress = true;
                }
                else
                {
                    SendMessageToClient($"{ServerToClientSignifiers.GameStartFail}, Room is not full", Room.hostUserID, pipeline);
                }
                break;
            }
            case ClientToServerSignifiers.LeaveWaitingRoom:
            {
                GameRoom Room = GameRoomList.FirstOrDefault(x => x.name == csv[1]);
                if (Room == null) return; //if this runs we have a serious problem.
                if (clientConnectionID == Room.hostUserID || clientConnectionID == Room.clientUserID)
                {
                    SendMessageToClient($"{ServerToClientSignifiers.ReturnToLobby}, Returning to lobby", Room.hostUserID, pipeline);
                    SendMessageToClient($"{ServerToClientSignifiers.ReturnToLobby}, Returning to lobby", Room.clientUserID, pipeline);
                    foreach (var ID in Room.observerIDs) { SendMessageToClient($"{ServerToClientSignifiers.ReturnToLobby}, Returning to lobby", ID, pipeline); }
                        GameRoomList.Remove(Room);
                }
                else
                {
                    SendMessageToClient($"{ServerToClientSignifiers.ReturnToLobby}, Returning to lobby", clientConnectionID, pipeline);
                    Room.observerIDs.Remove(clientConnectionID);
                }


                break;
            }
            case ClientToServerSignifiers.GameMove:
            {
                    GameRoom Room = GameRoomList.FirstOrDefault(x => x.name == csv[1]);
                    if (Room == null) return; //if this runs we have a serious problem.

                    if (Room.tiles[int.Parse(csv[2])] != 0)
                    {
                        //send message invalid move
                        if (clientConnectionID == Room.clientUserID || clientConnectionID == Room.hostUserID) SendMessageToClient($"{ServerToClientSignifiers.InvalidMove},Invalid move", clientConnectionID, pipeline);
                        else SendMessageToClient($"{ServerToClientSignifiers.InvalidMove},Observers cannot interact with the board", clientConnectionID, pipeline);
                        return;
                    }

                    if (Room.isHostTurn && clientConnectionID == Room.hostUserID)
                    {
                        Room.tiles[int.Parse(csv[2])] = 1;
                        Room.isHostTurn = false;
                        SendMessageToClient($"{ServerToClientSignifiers.SuccessfulMove},{LoggedInUsers[Room.clientUserID]},{csv[2]}", Room.hostUserID, pipeline);
                        SendMessageToClient($"{ServerToClientSignifiers.SuccessfulOpponentMove},{LoggedInUsers[Room.clientUserID]},{csv[2]}", Room.clientUserID, pipeline);
                        foreach (var ID in Room.observerIDs) { SendMessageToClient($"{ServerToClientSignifiers.SuccessfulOpponentMove},{LoggedInUsers[Room.clientUserID]},{csv[2]}", ID, pipeline); }
                    }
                    else if (!Room.isHostTurn && clientConnectionID == Room.clientUserID)
                    {
                        Room.tiles[int.Parse(csv[2])] = 2;
                        Room.isHostTurn = true;
                        SendMessageToClient($"{ServerToClientSignifiers.SuccessfulMove},{LoggedInUsers[Room.hostUserID]},{csv[2]}", Room.clientUserID, pipeline);
                        SendMessageToClient($"{ServerToClientSignifiers.SuccessfulOpponentMove},{LoggedInUsers[Room.hostUserID]},{csv[2]}", Room.hostUserID, pipeline);
                        foreach (var ID in Room.observerIDs) { SendMessageToClient($"{ServerToClientSignifiers.SuccessfulMove},{LoggedInUsers[Room.hostUserID]},{csv[2]}", ID, pipeline); }
                    }
                    else
                    {
                        if(clientConnectionID == Room.clientUserID || clientConnectionID == Room.hostUserID) SendMessageToClient($"{ServerToClientSignifiers.NotYourTurn},It is not your turn", clientConnectionID, pipeline);
                        else SendMessageToClient($"{ServerToClientSignifiers.NotYourTurn},Observers cannot interact with the board", clientConnectionID, pipeline);
                    }

                    if(gameLogic.CheckWinner(Room.tiles) == 1)
                    {
                        SendMessageToClient($"{ServerToClientSignifiers.GameWin},You Win", Room.hostUserID, pipeline);
                        SendMessageToClient($"{ServerToClientSignifiers.GameLose},You Lose", Room.clientUserID, pipeline);
                    }
                    else if (gameLogic.CheckWinner(Room.tiles) == 2)
                    {
                        SendMessageToClient($"{ServerToClientSignifiers.GameWin},You Win", Room.clientUserID, pipeline);
                        SendMessageToClient($"{ServerToClientSignifiers.GameLose},You Lose", Room.hostUserID, pipeline);
                    }
                    break;
            }
            case ClientToServerSignifiers.LeaveGame:
            {
                GameRoom Room = GameRoomList.FirstOrDefault(x => x.name == csv[1]);
                if (Room == null) return; //if this runs we have a serious problem.
                if (clientConnectionID == Room.hostUserID)
                {
                    SendMessageToClient($"{ServerToClientSignifiers.ReturnToLobby}, Returning to lobby", Room.hostUserID, pipeline);
                    SendMessageToClient($"{ServerToClientSignifiers.OpponentDisconnected}, {LoggedInUsers[Room.hostUserID]} Disconnected", Room.clientUserID, pipeline);
                    foreach (var ID in Room.observerIDs) { SendMessageToClient($"{ServerToClientSignifiers.OpponentDisconnected}, {LoggedInUsers[Room.hostUserID]} Disconnected", ID, pipeline); }
                    GameRoomList.Remove(Room);

                }
                else if (clientConnectionID == Room.clientUserID)
                {
                    SendMessageToClient($"{ServerToClientSignifiers.ReturnToLobby}, Returning to lobby", Room.clientUserID, pipeline);
                    SendMessageToClient($"{ServerToClientSignifiers.OpponentDisconnected}, {LoggedInUsers[Room.clientUserID]} Disconnected", Room.hostUserID, pipeline);
                    foreach (var ID in Room.observerIDs) { SendMessageToClient($"{ServerToClientSignifiers.OpponentDisconnected}, {LoggedInUsers[Room.clientUserID]} Disconnected", ID, pipeline); }
                    GameRoomList.Remove(Room);
                }
                else
                {
                    SendMessageToClient($"{ServerToClientSignifiers.ReturnToLobby}, Returning to lobby", clientConnectionID, pipeline);
                    Room.observerIDs.Remove(clientConnectionID);
                }

                break;
            }
            case ClientToServerSignifiers.PlayerMessage:
            {
                GameRoom Room = GameRoomList.FirstOrDefault(x => x.name == csv[1]);
                if (Room == null) return; //if this runs we have a serious problem.
                if (clientConnectionID == Room.hostUserID)
                {
                        SendMessageToClient($"{ServerToClientSignifiers.DisplayHostMessage},{csv[2]}", Room.clientUserID, pipeline);
                        SendMessageToClient($"{ServerToClientSignifiers.DisplayHostMessage},{csv[2]}", Room.hostUserID, pipeline);
                        foreach (var ID in Room.observerIDs) SendMessageToClient($"{ServerToClientSignifiers.DisplayHostMessage},{csv[2]}", ID, pipeline);
                }
                else if (clientConnectionID == Room.clientUserID)
                {
                    SendMessageToClient($"{ServerToClientSignifiers.DisplayClientMessage},{csv[2]}", Room.clientUserID, pipeline);
                    SendMessageToClient($"{ServerToClientSignifiers.DisplayClientMessage},{csv[2]}", Room.hostUserID, pipeline);
                    foreach (var ID in Room.observerIDs) SendMessageToClient($"{ServerToClientSignifiers.DisplayClientMessage},{csv[2]}", ID, pipeline);
                }
                break;
            }
        }

    }
    static public void SendMessageToClient(string msg, int clientConnectionID, TransportPipeline pipeline)
    {
        networkServer.SendMessageToClient(msg, clientConnectionID, pipeline);
    }

    #endregion

    #region Connection Events

    static public void ConnectionEvent(int clientConnectionID)
    {
        Debug.Log("Client connection, ID == " + clientConnectionID);
    }
    static public void DisconnectionEvent(int clientConnectionID)
    {
        Debug.Log("Client disconnection, ID == " + clientConnectionID);
        if(LoggedInUsers.ContainsKey(clientConnectionID)) LoggedInUsers.Remove(clientConnectionID);
    }

    #endregion

    #region Setup
    static NetworkServer networkServer;
    static GameLogic gameLogic;

    static public void SetNetworkServer(NetworkServer NetworkServer)
    {
        networkServer = NetworkServer;
    }
    static public NetworkServer GetNetworkServer()
    {
        return networkServer;
    }
    static public void SetGameLogic(GameLogic GameLogic)
    {
        gameLogic = GameLogic;
    }

    #endregion
}

#region Protocol Signifiers
static public class ClientToServerSignifiers
{
    public const int Login = 1;
    public const int CreateAccount = 2;
    public const int FindGame = 3;
    public const int GameStart = 4;
    public const int LeaveWaitingRoom = 5;
    public const int LeaveGame = 6;
    public const int GameMove = 7;
    public const int PlayerMessage = 8;
}

static public class ServerToClientSignifiers
{
    public const int LoginFail = 0;
    public const int LoginSuccess = 1;
    public const int UserNotExist = 2;
    public const int UsernameInUse = 3;
    public const int FormatError = 4;
    public const int AccountCreated = 5;
    public const int NewGameRoom = 6;
    public const int ReturnToLobby = 7;
    public const int GameRoomFound = 8;
    public const int ClientJoined = 9;
    public const int ClientLeft = 10;//no need to use anymore
    public const int FullGameRoomFound = 11;
    public const int GameStartSuccess = 12;
    public const int GameStartFail = 13;
    public const int FullGameRoomFoundInProgress = 14;
    public const int InvalidMove = 15;
    public const int SuccessfulMove = 16;
    public const int SuccessfulOpponentMove = 17;
    public const int NotYourTurn = 18;
    public const int GameWin = 19;
    public const int GameLose = 20;
    public const int OpponentDisconnected = 21;
    public const int DisplayHostMessage = 22;
    public const int DisplayClientMessage = 23;
}

#endregion

