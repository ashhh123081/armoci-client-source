using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Vape
{
    internal static class VapeZeusServer
    {
        private static TcpListener listener;
        private static volatile bool running;
        private static readonly object gate = new object();
        private static readonly List<ZeusSession> sessions = new List<ZeusSession>();

        public static bool IsRunning
        {
            get
            {
                return running;
            }
        }

        public static void Start()
        {
            if (running)
            {
                return;
            }
            try
            {
                listener = new TcpListener(IPAddress.Any, 8091);
                listener.Start();
            }
            catch (Exception)
            {
                running = false;
                return;
            }
            running = true;
            VapeTrace.Write("zeus server listening on " + VapeLanAddress.Get()
                    + ":8091 (and 127.0.0.1:8091)");
            Thread acceptThread = new Thread(VapeZeusServer.AcceptLoop);
            acceptThread.IsBackground = true;
            acceptThread.Start();
        }

        private static void AcceptLoop()
        {
            while (running)
            {
                Socket socket;
                try
                {
                    socket = listener.AcceptSocket();
                }
                catch (Exception)
                {
                    break;
                }
                ZeusSession session = new ZeusSession(socket);
                lock (gate)
                {
                    sessions.Add(session);
                }
                Thread handler = new Thread(session.Run);
                handler.IsBackground = true;
                handler.Start();
            }
        }

        internal static void RegisterSession(ZeusSession session)
        {
            lock (gate)
            {
                for (int index = sessions.Count - 1; index >= 0; --index)
                {
                    ZeusSession other = sessions[index];
                    if (other.userId == session.userId && other != session)
                    {
                        try
                        {
                            other.SendDisconnect(1);
                            other.Close();
                        }
                        catch (Exception)
                        {
                        }
                        sessions.RemoveAt(index);
                    }
                }
            }
        }

        internal static void RemoveSession(ZeusSession session)
        {
            lock (gate)
            {
                sessions.Remove(session);
            }
        }

        internal static ZeusSession FindSession(long userId)
        {
            lock (gate)
            {
                foreach (ZeusSession session in sessions)
                {
                    if (session.userId == userId && session.authenticated)
                    {
                        return session;
                    }
                }
                return null;
            }
        }

        internal class ZeusSession
        {
            private readonly Socket socket;
            private readonly byte[] chunk = new byte[8192];
            private readonly MemoryStream inbound = new MemoryStream();
            internal volatile bool authenticated;
            internal long userId;
            internal string username;

            internal ZeusSession(Socket socket)
            {
                this.socket = socket;
            }

            internal void Close()
            {
                try
                {
                    socket.Close();
                }
                catch (Exception)
                {
                }
            }

            internal void Run()
            {
                try
                {
                    while (true)
                    {
                        int read = socket.Receive(chunk);
                        if (read <= 0)
                        {
                            break;
                        }
                        inbound.Write(chunk, 0, read);
                        ProcessBuffer();
                    }
                }
                catch (Exception)
                {
                }
                VapeZeusServer.RemoveSession(this);
            }

            private void ProcessBuffer()
            {
                while (true)
                {
                    byte[] bytes = inbound.ToArray();
                    int prefixSize;
                    int? length = ZeusWire.PeekFrameLength(bytes, out prefixSize);
                    if (length == null || bytes.Length < prefixSize + (int)length)
                    {
                        return;
                    }
                    byte[] frame = new byte[(int)length];
                    Array.Copy(bytes, prefixSize, frame, 0, (int)length);
                    inbound.SetLength(0);
                    if (bytes.Length > prefixSize + (int)length)
                    {
                        inbound.Write(bytes, prefixSize + (int)length,
                                bytes.Length - prefixSize - (int)length);
                    }
                    HandleFrame(frame);
                }
            }

            private void HandleFrame(byte[] frame)
            {
                try
                {
                    ZeusWire.Reader reader = new ZeusWire.Reader(frame);
                    int packetId = reader.ReadVarInt();
                    if (!this.authenticated)
                    {
                        HandleUnauthenticated(packetId, reader);
                    }
                    else
                    {
                        HandleAuthenticated(packetId, reader);
                    }
                }
                catch (Exception)
                {
                }
            }

            private void HandleUnauthenticated(int packetId, ZeusWire.Reader reader)
            {
                switch (packetId)
                {
                case 0:
                    {
                        byte[] requestId = reader.ReadUuidBytes();
                        this.Send(0, delegate(ZeusWire.Writer writer)
                        {
                            writer.WriteUuidBytes(requestId);
                        });
                        break;
                    }
                case 1:
                    {
                        byte[] requestId = reader.ReadUuidBytes();
                        int protocolVersion = reader.ReadVarInt();
                        this.Send(2, delegate(ZeusWire.Writer writer)
                        {
                            writer.WriteUuidBytes(requestId);
                            writer.WriteVarInt(0);
                        });
                        break;
                    }
                case 2:
                    {
                        byte[] requestId = reader.ReadUuidBytes();
                        string token = reader.ReadString(255);
                        byte[] sessionId = reader.ReadUuidBytes();
                        string version = reader.ReadString(16);
                        VapeStore.EnsureLoaded();
                        VapeStore.Profile profile = VapeStore.GetLocalProfile();
                        long profileId = profile != null ? profile.id : 1L;
                        string profileName = profile != null ? profile.username : "Player";
                        this.userId = profileId;
                        this.username = profileName;
                        this.Send(3, delegate(ZeusWire.Writer writer)
                        {
                            writer.WriteUuidBytes(requestId);
                            writer.WriteVarInt(1);
                            writer.WriteLong(profileId);
                            writer.WriteString(profileName);
                        });
                        this.authenticated = true;
                        VapeZeusServer.RegisterSession(this);
                        VapeTrace.Write("zeus authenticated: " + profileName + " (#" + profileId + ")");
                        break;
                    }
                default:
                    break;
                }
            }

            private void HandleAuthenticated(int packetId, ZeusWire.Reader reader)
            {
                switch (packetId)
                {
                case 0:
                    {
                        byte[] requestId = reader.ReadUuidBytes();
                        this.Send(0, delegate(ZeusWire.Writer writer)
                        {
                            writer.WriteUuidBytes(requestId);
                        });
                        break;
                    }
                case 3:
                    {
                        byte[] requestId = reader.ReadUuidBytes();
                        this.Send(3, delegate(ZeusWire.Writer writer)
                        {
                            writer.WriteUuidBytes(requestId);
                            List<VapeStore.Profile> friends = VapeStore.GetFriends(this.userId);
                            writer.WriteInt(friends.Count);
                            foreach (VapeStore.Profile friend in friends)
                            {
                                WriteFriendModel(writer, friend, true);
                            }
                            List<VapeStore.FriendRequestRecord> incoming =
                                    VapeStore.GetIncomingRequests(this.userId);
                            writer.WriteInt(incoming.Count);
                            foreach (VapeStore.FriendRequestRecord request in incoming)
                            {
                                WriteRequestModel(writer, request);
                            }
                            List<VapeStore.FriendRequestRecord> outgoing =
                                    VapeStore.GetOutgoingRequests(this.userId);
                            writer.WriteInt(outgoing.Count);
                            foreach (VapeStore.FriendRequestRecord request in outgoing)
                            {
                                WriteRequestModel(writer, request);
                            }
                        });
                        break;
                    }
                case 4:
                    {
                        byte[] requestId = reader.ReadUuidBytes();
                        long targetId = reader.ReadLong();
                        string targetName = reader.ReadString(16);
                        VapeStore.RemoveFriendship(this.userId, targetId);
                        this.Send(5, delegate(ZeusWire.Writer writer)
                        {
                            writer.WriteUuidBytes(requestId);
                            writer.WriteBoolean(true);
                        });
                        ZeusSession other = VapeZeusServer.FindSession(targetId);
                        if (other != null)
                        {
                            other.Send(6, delegate(ZeusWire.Writer writer)
                            {
                                writer.WriteLong(this.userId);
                                writer.WriteString(this.username);
                            });
                        }
                        break;
                    }
                case 5:
                    {
                        byte[] requestId = reader.ReadUuidBytes();
                        string targetUsername = reader.ReadString(16);
                        VapeStore.EnsureLoaded();
                        VapeStore.Profile self = VapeStore.FindProfileById(this.userId);
                        VapeStore.Profile target = VapeStore.FindProfileByUsername(targetUsername);
                        int status;
                        VapeStore.FriendRequestRecord created = null;
                        if (target == null || self == null)
                        {
                            status = 4;
                        }
                        else if (target.id == self.id)
                        {
                            status = 1;
                        }
                        else if (VapeStore.AreFriends(self.id, target.id))
                        {
                            status = 2;
                        }
                        else if (VapeStore.FindRequest(self.id, target.id) != null
                                || VapeStore.FindRequest(target.id, self.id) != null)
                        {
                            status = 3;
                        }
                        else
                        {
                            created = VapeStore.CreateRequest(self.id, target.id);
                            status = 0;
                        }
                        VapeStore.FriendRequestRecord createdFinal = created;
                        this.Send(10, delegate(ZeusWire.Writer writer)
                        {
                            writer.WriteUuidBytes(requestId);
                            writer.WriteVarInt(status);
                            if (status == 0 && createdFinal != null)
                            {
                                ZeusSession.WriteRequestModel(writer, createdFinal);
                            }
                        });
                        if (createdFinal != null)
                        {
                            ZeusSession targetSession = VapeZeusServer.FindSession(target.id);
                            if (targetSession != null)
                            {
                                targetSession.Send(8, delegate(ZeusWire.Writer writer)
                                {
                                    ZeusSession.WriteRequestModel(writer, createdFinal);
                                });
                            }
                        }
                        break;
                    }
                case 6:
                    {
                        byte[] requestId = reader.ReadUuidBytes();
                        long requesterId = reader.ReadLong();
                        bool accepted = reader.ReadBoolean();
                        VapeStore.FriendRequestRecord request = VapeStore.FindRequest(
                                requesterId, this.userId);
                        if (request == null)
                        {
                            this.Send(11, delegate(ZeusWire.Writer writer)
                            {
                                writer.WriteUuidBytes(requestId);
                                writer.WriteLong(0L);
                                writer.WriteVarInt(2);
                            });
                            break;
                        }
                        VapeStore.RemoveRequest(request.id);
                        if (accepted)
                        {
                            VapeStore.AddFriendship(requesterId, this.userId);
                            VapeStore.Profile newFriend = VapeStore.FindProfileById(requesterId);
                            this.Send(11, delegate(ZeusWire.Writer writer)
                            {
                                writer.WriteUuidBytes(requestId);
                                writer.WriteLong(request.id);
                                writer.WriteVarInt(0);
                                ZeusSession.WriteFriendModel(writer, newFriend, true);
                            });
                            ZeusSession other = VapeZeusServer.FindSession(requesterId);
                            if (other != null)
                            {
                                VapeStore.Profile self = VapeStore.FindProfileById(this.userId);
                                other.Send(4, delegate(ZeusWire.Writer writer)
                                {
                                    ZeusSession.WriteFriendModel(writer, self, true);
                                });
                            }
                        }
                        else
                        {
                            this.Send(11, delegate(ZeusWire.Writer writer)
                            {
                                writer.WriteUuidBytes(requestId);
                                writer.WriteLong(request.id);
                                writer.WriteVarInt(1);
                            });
                        }
                        break;
                    }
                case 19:
                    {
                        byte[] minecraftUuid = reader.ReadUuidBytes();
                        string minecraftUsername = reader.ReadString(16);
                        if (minecraftUsername != null && minecraftUsername.Length > 0)
                        {
                            VapeStore.EnsureLoaded();
                            VapeStore.Profile named =
                                    VapeStore.FindProfileByUsername(minecraftUsername);
                            if (named != null)
                            {
                                VapeStore.SetActiveProfile(named.id);
                                this.userId = named.id;
                                this.username = named.username;
                            }
                            else
                            {
                                VapeStore.Profile current =
                                        VapeStore.FindProfileById(this.userId);
                                if (current != null)
                                {
                                    VapeStore.RenameProfile(current.id, minecraftUsername);
                                    this.username = minecraftUsername;
                                }
                                else
                                {
                                    VapeStore.Profile created =
                                            VapeStore.CreateProfile(minecraftUsername);
                                    this.userId = created.id;
                                    this.username = created.username;
                                }
                            }
                            VapeStore.SetMinecraftUser(this.userId, minecraftUsername);
                            VapeTrace.Write("zeus minecraft profile: vape online name is now "
                                    + minecraftUsername);
                        }
                        break;
                    }
                case 30:
                    {
                        VapeTrace.Write("zeus client ready: " + this.username);
                        break;
                    }
                default:
                    break;
                }
            }

            private static void WriteFriendModel(ZeusWire.Writer writer,
                    VapeStore.Profile friend, bool visible)
            {
                writer.WriteLong(friend.id);
                writer.WriteString(friend.username);
                writer.WriteUuidBytes(Guid.NewGuid().ToByteArray());
                writer.WriteString("");
                writer.WriteBoolean(visible);
                writer.WriteVarInt(0);
                writer.WriteBoolean(false);
            }

            private static void WriteRequestModel(ZeusWire.Writer writer,
                    VapeStore.FriendRequestRecord request)
            {
                VapeStore.Profile requester = VapeStore.FindProfileById(request.requester);
                VapeStore.Profile recipient = VapeStore.FindProfileById(request.recipient);
                writer.WriteLong(request.id);
                writer.WriteLong(requester != null ? requester.id : 0L);
                writer.WriteString(requester != null ? requester.username : "?");
                writer.WriteLong(recipient != null ? recipient.id : 0L);
                writer.WriteString(recipient != null ? recipient.username : "?");
            }

            internal void SendDisconnect(int reasonOrdinal)
            {
                this.Send(1, delegate(ZeusWire.Writer writer)
                {
                    writer.WriteVarInt(reasonOrdinal);
                });
            }

            internal void Send(int packetId, ZeusWire.Writer.Body body)
            {
                try
                {
                    byte[] payload = ZeusWire.Writer.Build(packetId, body);
                    ZeusWire.Writer frame = new ZeusWire.Writer();
                    frame.WriteVarInt(payload.Length);
                    socket.Send(frame.ToArray(payload));
                }
                catch (Exception)
                {
                }
            }
        }
    }

    internal static class ZeusWire
    {
        internal class Reader
        {
            private readonly byte[] data;
            private int position;

            internal Reader(byte[] data)
            {
                this.data = data;
            }

            internal int ReadVarInt()
            {
                int result = 0;
                int shift = 0;
                byte current;
                do
                {
                    current = this.data[this.position++];
                    result |= (current & 0x7F) << shift++ * 7;
                }
                while ((current & 0x80) == 128 && shift < 5);
                return result;
            }

            internal long ReadLong()
            {
                long value = 0L;
                for (int index = 0; index < 8; ++index)
                {
                    value = (value << 8) | (long)(this.data[this.position++] & 0xFF);
                }
                return value;
            }

            internal bool ReadBoolean()
            {
                return this.data[this.position++] != 0;
            }

            internal string ReadString(int maxCharacters)
            {
                int length = this.ReadVarInt();
                string text = Encoding.UTF8.GetString(this.data, this.position, length);
                this.position += length;
                return text;
            }

            internal byte[] ReadUuidBytes()
            {
                byte[] uuid = new byte[16];
                Array.Copy(this.data, this.position, uuid, 0, 16);
                this.position += 16;
                return uuid;
            }
        }

        internal class Writer
        {
            internal delegate void Body(Writer writer);

            private readonly MemoryStream stream = new MemoryStream();

            internal void WriteVarInt(int value)
            {
                uint unsigned = (uint)value;
                while ((unsigned & 0xFFFFFF80u) != 0u)
                {
                    this.stream.WriteByte((byte)(unsigned & 0x7Fu | 0x80u));
                    unsigned >>= 7;
                }
                this.stream.WriteByte((byte)unsigned);
            }

            internal void WriteInt(int value)
            {
                this.WriteByte((byte)(value >> 24));
                this.WriteByte((byte)(value >> 16));
                this.WriteByte((byte)(value >> 8));
                this.WriteByte((byte)value);
            }

            internal void WriteLong(long value)
            {
                for (int shift = 56; shift >= 0; shift -= 8)
                {
                    this.WriteByte((byte)(value >> shift));
                }
            }

            internal void WriteBoolean(bool value)
            {
                this.stream.WriteByte(value ? (byte)1 : (byte)0);
            }

            internal void WriteString(string text)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text ?? "");
                this.WriteVarInt(bytes.Length);
                this.stream.Write(bytes, 0, bytes.Length);
            }

            internal void WriteUuidBytes(byte[] uuid)
            {
                this.stream.Write(uuid, 0, 16);
            }

            private void WriteByte(byte value)
            {
                this.stream.WriteByte(value);
            }

            internal byte[] ToArray()
            {
                return this.stream.ToArray();
            }

            internal byte[] ToArray(byte[] suffix)
            {
                this.stream.Write(suffix, 0, suffix.Length);
                return this.stream.ToArray();
            }

            internal static byte[] Build(int packetId, Body body)
            {
                Writer payload = new Writer();
                payload.WriteVarInt(packetId);
                body(payload);
                return payload.ToArray();
            }
        }

        internal static int? PeekFrameLength(byte[] bytes, out int prefixSize)
        {
            int length = 0;
            int shift = 0;
            for (int index = 0; index < Math.Min(3, bytes.Length); ++index)
            {
                byte current = bytes[index];
                length |= (current & 0x7F) << shift++ * 7;
                if ((current & 0x80) == 0)
                {
                    prefixSize = index + 1;
                    return length;
                }
            }
            prefixSize = 0;
            return null;
        }
    }
}
