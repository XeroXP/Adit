﻿using Adit.Models;
using Adit.Code.Shared;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Adit.Code.Server
{
    public class ServerSocketMessages : SocketMessageHandler
    {
        public ClientConnection ConnectionToClient { get; set; }
        public ClientSession Session { get; set; }
        public ServerSocketMessages(ClientConnection connection)
            : base(connection.Socket)
        {
            this.ConnectionToClient = connection;
        }

        private async void ReceiveConnectionType(dynamic jsonData)
        {
            switch (jsonData["ConnectionType"])
            {
                case "Client":
                    ConnectionToClient.ConnectionType = ConnectionTypes.Client;
                    Session = new ClientSession();
                    Session.ConnectedClients.Add(ConnectionToClient);
                    AditServer.SessionList.Add(Session);
                    await SendSessionID();
                    break;
                case "ElevatedClient":
                    ConnectionToClient.ConnectionType = ConnectionTypes.ElevatedClient;
                    Session = AditServer.SessionList.Find(x => x.SessionID == jsonData["SessionID"]);
                    if (Session == null)
                    {
                        Session = new ClientSession();
                        Session.SessionID = jsonData["SessionID"];
                    }
                    Session.ConnectedClients.Add(ConnectionToClient);
                    AditServer.SessionList.Add(Session);
                    break;
                case "Viewer":
                    ConnectionToClient.ConnectionType = ConnectionTypes.Viewer;
                    await SendReadyForViewer();
                    break;
                case "Service":
                    ConnectionToClient.ConnectionType = ConnectionTypes.Service;
                    Session = new ClientSession();
                    Session.SessionID = Guid.NewGuid().ToString();
                    Session.ConnectedClients.Add(ConnectionToClient);
                    AditServer.SessionList.Add(Session);
                    break;
                default:
                    break;
            }
        }

        private async Task SendSessionID()
        {
            await SendJSON( new { Type = "SessionID", SessionID = Session.SessionID });
        }
        private async Task SendReadyForViewer()
        {
            await SendJSON(new { Type = "ReadyForViewer" });
        }
        private async void ReceiveViewerConnectRequest(dynamic jsonData)
        {
            var session = AditServer.SessionList.Find(x => x.SessionID.Replace(" ", "") == jsonData["SessionID"].Replace(" ", ""));
            if (session == null)
            {
                jsonData["Status"] = "notfound";
                SendJSON(jsonData);
                return;
            }
            if (session.ConnectedClients[0]?.ConnectionType == ConnectionTypes.Service)
            {
                await SendRequestForElevatedClient(session.ConnectedClients[0]);
                return;
            }
            session.ConnectedClients.Add(ConnectionToClient);
            await SendParticipantList(session);
            Session = session;
            jsonData["Status"] = "ok";
            await SendJSON(jsonData);
        }

        public async Task SendEncryptionStatus()
        {
            try
            {
                if (Config.Current.IsEncryptionEnabled)
                {
                    using (var httpClient = new System.Net.Http.HttpClient())
                    {
                        var response = await httpClient.GetAsync("https://aditapi.azurewebsites.net/api/keys");
                        var content = await response.Content.ReadAsStringAsync();
                        var key = Utilities.JSON.Deserialize<string[]>(content);
                        await SendJSON(new
                        {
                            Type = "EncryptionStatus",
                            Status = "On",
                            ID = key[0]
                        });
                        Encryption = new Encryption();
                        Encryption.Key = Convert.FromBase64String(key[1]);
                    }
                }
                else
                {
                    await SendJSON(new
                    {
                        Type = "EncryptionStatus",
                        Status = "Off"
                    });
                }
            }
            catch (Exception ex)
            {
                Utilities.WriteToLog(ex);
                await SendJSON(new
                {
                    Type = "EncryptionStatus",
                    Status = "Failed"
                });
            }
        }

        private async Task SendRequestForElevatedClient(ClientConnection clientConnection)
        {
            var request = new
            {
                Type = "RequestForElevatedClient",
                RequesterID = this.ConnectionToClient.ID
            };
            await clientConnection.SendJSON(request);
        }
        private async void ReceiveRequestForElevatedClient(dynamic jsonData)
        {
            if (jsonData["Status"] == "ok")
            {
                var startWait = DateTime.Now;
                while (AditServer.ClientList.Any(x => x.SessionID == jsonData["ClientSessionID"]) == false)
                {
                    await Task.Delay(500);
                    if (DateTime.Now - startWait > TimeSpan.FromSeconds(5))
                    {
                        jsonData["Status"] = "failed";
                        break;
                    }
                }
            }
            var requester = AditServer.ClientList.Find(x => x.ID == jsonData["RequesterID"]);
            requester.SendJSON(jsonData);
        }
        private async void ReceiveDesktopSwitch(dynamic jsonData)
        {
            var session  = ConnectionToClient.SocketMessageHandler.Session;
            var startWait = DateTime.Now;
            jsonData["Status"] = "ok";

            while (AditServer.ClientList.Any(x => x.ConnectionType == ConnectionTypes.ElevatedClient && x.ID != ConnectionToClient.ID) == false)
            {
                await Task.Delay(500);
                if (DateTime.Now - startWait > TimeSpan.FromSeconds(5))
                {
                    jsonData["Status"] = "failed";
                    break;
                }
            }
            await SendParticipantList(session);
            foreach (var client in session.ConnectedClients.ToList())
            {
                client.SendJSON(jsonData);
            }
            ConnectionToClient.Socket.Shutdown(SocketShutdown.Both);
            ConnectionToClient.Socket.Disconnect(false);
        }
        public async Task SendParticipantList(ClientSession session)
        {
            foreach (var connection in session.ConnectedClients)
            {
                await connection.SendJSON(new
                {
                    Type = "ParticipantList",
                    ParticipantList = session.ConnectedClients.Select(x=>x.ID)
                });
            }
        }
        private void ReceiveImageRequest(dynamic jsonData)
        {
            jsonData["RequesterID"] = ConnectionToClient.ID;
            var clients = Session.ConnectedClients.FindAll(x => x.ConnectionType == ConnectionTypes.Client || x.ConnectionType == ConnectionTypes.ElevatedClient);
            foreach (var client in clients)
            {
                client.SocketMessageHandler.LastRequesterID = ConnectionToClient.ID;
                client.SendJSON(jsonData);
            }
        }
        private void ReceiveSAS(dynamic jsonData)
        {
            var service = AditServer.ClientList.Find(x => x.ConnectionType == ConnectionTypes.Service && x.MACAddress == jsonData["MAC"]);
            service.SendJSON(jsonData);
        }

        private void ReceiveHeartbeat(dynamic jsonData)
        {
            ConnectionToClient.ComputerName = jsonData["ComputerName"]?.Trim();
            ConnectionToClient.LastReboot = jsonData["LastReboot"];
            ConnectionToClient.CurrentUser = jsonData["CurrentUser"];
            ConnectionToClient.MACAddress = jsonData["MACAddress"];
            ComputerHub.Current.AddOrUpdateComputer(ConnectionToClient);
        }
        private void ReceiveHubDataRequest(dynamic jsonData)
        {
            if (Authentication.Current.Keys.Count == 0)
            {
                jsonData["Status"] = "Server has no authentication keys.  Create an authentication key on the server first.";
            }
            else if (!Authentication.Current.Keys.Any(x=>x.Key == jsonData["Key"]?.Trim()?.ToLower()))
            {
                jsonData["Status"] = "Authentication key wasn't found.";
            }
            else
            {
                var key = Authentication.Current.Keys.FirstOrDefault(x => x.Key == jsonData["Key"]?.Trim()?.ToLower());
                key.LastUsed = DateTime.Now;
                Authentication.Current.Save();
                jsonData["Status"] = "ok";
                MainWindow.Current.Dispatcher.Invoke(() => {
                    ComputerHub.Current.Load();
                });
                jsonData["ComputerList"] = ComputerHub.Current.ComputerList;
            }
            SendJSON(jsonData);
        }
        private void ReceiveBinaryTransferStarting(dynamic jsonData)
        {
            jsonData["Sender"] = ConnectionToClient.ID;
            if (Enum.Parse(typeof(BinaryTransferType), jsonData["TransferType"]) == BinaryTransferType.ScreenCapture)
            {
                foreach (var viewer in Session.ConnectedClients.FindAll(x => x.ConnectionType == ConnectionTypes.Viewer))
                {
                    viewer.SendJSON(jsonData);
                }
            }
        }
        private async void ReceiveByteArray(byte[] bytesReceived)
        {
            if (ConnectionToClient.ConnectionType == ConnectionTypes.Client || ConnectionToClient.ConnectionType == ConnectionTypes.ElevatedClient)
            {
                if (string.IsNullOrWhiteSpace(LastRequesterID))
                {
                    Utilities.WriteToLog("Requester ID is blank.");
                    return;
                }
                var requester = AditServer.ClientList.Find(x => x.ID == LastRequesterID);
                if (requester == null)
                {
                    Utilities.WriteToLog($"Requester not found based on ID {LastRequesterID}.");
                    return;
                }
                await requester.SendBytes(bytesReceived);
            }
            else if (ConnectionToClient.ConnectionType == ConnectionTypes.Viewer)
            {
                var session = AditServer.SessionList.Find(x => x.SessionID == ConnectionToClient.SessionID);
                var client = Session.ConnectedClients.Find(x => x.ConnectionType == ConnectionTypes.Client || x.ConnectionType == ConnectionTypes.ElevatedClient);
                await client.SendBytes(bytesReceived);
            }
        }
    }
}
