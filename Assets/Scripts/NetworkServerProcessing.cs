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
                            SendMessageToClient($"{ServerToClientSignifiers.LoginSuccess},login successful", clientConnectionID, pipeline);
                            return;
                        }
                        SendMessageToClient($"{ServerToClientSignifiers.LoginFail},Incorrect Password", clientConnectionID, pipeline);
                        return;
                    }
                }
                SendMessageToClient($"{ServerToClientSignifiers.UserNotExist},user does not exist", clientConnectionID, pipeline);
                break;
            }
            case ClientToServerSignifiers.createAccount:
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
            case ClientToServerSignifiers.findGame:
            {
                foreach (var Room in GameRoomList)
                {
                    if (Room.name != csv[1]) continue;
                    if (Room.isFull)
                    {
                            SendMessageToClient($"{ServerToClientSignifiers.GameRoomFull}, Existing game room with name is full", clientConnectionID, pipeline);
                            return;
                    }
                    else if (Room.isInProgress)
                    {
                        SendMessageToClient($"{ServerToClientSignifiers.GameRoomGameInProgress}, Existing game with name already in progress", clientConnectionID, pipeline);
                        return;
                    }
                    else
                    {
                        SendMessageToClient($"{ServerToClientSignifiers.GameRoomFound}, Existing game room found", clientConnectionID, pipeline);
                        Room.clientUserID = clientConnectionID;
                        Room.isFull = true;
                        SendMessageToClient($"{ServerToClientSignifiers.ClientJoined}, Client has joined", Room.hostUserID, pipeline);
                        return;
                    }
                }
                SendMessageToClient($"{ServerToClientSignifiers.NewGameRoom}, Creating new game room", clientConnectionID, pipeline);
                GameRoomList.AddLast(new GameRoom(csv[1], clientConnectionID));
                break;
            }
            case ClientToServerSignifiers.gameStart:
            {
                GameRoom Room = GameRoomList.FirstOrDefault(x => x.name == csv[1]);
                if (Room == null) return; //if this runs we have a serious problem.
                if (Room.isFull)
                {
                    SendMessageToClient($"{ServerToClientSignifiers.GameStartSuccess}, Starting Game", Room.hostUserID, pipeline);
                    SendMessageToClient($"{ServerToClientSignifiers.GameStartSuccess}, Starting Game", Room.clientUserID, pipeline);
                    Room.isInProgress = true;
                }
                else
                {
                    SendMessageToClient($"{ServerToClientSignifiers.GameStartFail}, Room is not full", Room.hostUserID, pipeline);
                }
                break;
            }
            case ClientToServerSignifiers.leaveWaitingRoom:
            {
                GameRoom Room = GameRoomList.FirstOrDefault(x => x.name == csv[1]);
                if (Room == null) return; //if this runs we have a serious problem.
                if(Room.isFull)SendMessageToClient($"{ServerToClientSignifiers.ReturnToLobby}, Returning to lobby", Room.clientUserID, pipeline);
                if (clientConnectionID == Room.hostUserID)
                {
                    SendMessageToClient($"{ServerToClientSignifiers.ReturnToLobby}, Returning to lobby", Room.hostUserID, pipeline);
                    GameRoomList.Remove(Room);
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
    public const int createAccount = 2;
    public const int findGame = 3;
    public const int gameStart = 4;
    public const int leaveWaitingRoom = 5;
    public const int leaveGame = 6;
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
    public const int GameRoomFull = 10;
    public const int GameStartSuccess = 11;
    public const int GameStartFail = 12;
    public const int GameRoomGameInProgress = 13;
}

#endregion

