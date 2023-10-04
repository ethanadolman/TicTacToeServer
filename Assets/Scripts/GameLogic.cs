using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Credentials
{
    public string username;
    public string password;

    public Credentials(string username, string password)
    {
        this.username = username;
        this.password = password;
    }
}
public class GameRoom
{
    public string name;
    public int hostUserID;
    public int clientUserID;
    public bool isInProgress;
    public bool isFull;

    public GameRoom(string name, int hostUser)
    {
        this.name = name;
        this.hostUserID = hostUser;
        isInProgress = false;
        isFull = false;
    }
}
public class GameLogic : MonoBehaviour
{
    public LinkedList<Credentials> UserDatabase = new LinkedList<Credentials>();
    public LinkedList<GameRoom> GameRoomList = new LinkedList<GameRoom>();
    void Start()
    {
        NetworkServerProcessing.SetGameLogic(this);
        LoadDatabase();
        NetworkServerProcessing.UserDatabase = UserDatabase;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
            NetworkServerProcessing.SendMessageToClient("Hello client's world, sincerely your network server", 0, TransportPipeline.ReliableAndInOrder);
    }

    void LoadDatabase()
    {
        string line;
        try
        {
            using (StreamReader reader = new StreamReader("UserDatabase.txt"))
            {

                while ((line = reader.ReadLine()) != null)
                {
                    string[] strlist = line.Split(' ');
                    
                    UserDatabase.AddLast(new Credentials(strlist[0], strlist[1]));
                            
                    
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading file: {ex.Message}");

        }
    }

    void SaveDatabase()
    {
        using (StreamWriter writer = new StreamWriter("UserDatabase.txt"))
        {
            foreach (var User in UserDatabase)
            {
                writer.WriteLine($"{User.username} {User.password}");
            }
        }
    }

    private void OnDestroy()
    {
        SaveDatabase();
    }
}
