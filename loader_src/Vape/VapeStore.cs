using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace Vape
{
    internal class VapeStore
    {
        internal class Profile
        {
            public long id { get; set; }
            public string username { get; set; }
            public string password { get; set; }
            public string minecraftUsername { get; set; }
            public string createdAt { get; set; }
        }

        internal class Friendship
        {
            public long a { get; set; }
            public long b { get; set; }
        }

        internal class FriendRequestRecord
        {
            public long id { get; set; }
            public long requester { get; set; }
            public long recipient { get; set; }
        }

        internal class PublicConfig
        {
            public long id { get; set; }
            public long ownerId { get; set; }
            public string ownerName { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public List<string> tags { get; set; }
            public string data { get; set; }
            public long downloads { get; set; }
            public long likes { get; set; }
            public long version { get; set; }
            public string createdAt { get; set; }
        }

        internal class StoreData
        {
            public List<Profile> profiles { get; set; }
            public List<Friendship> friendships { get; set; }
            public List<FriendRequestRecord> requests { get; set; }
            public List<PublicConfig> configs { get; set; }
            public long activeProfile { get; set; }
            public Dictionary<string, string> settings { get; set; }
            public Dictionary<string, string> privateProfiles { get; set; }
        }

        private static readonly object gate = new object();
        private static StoreData data;

        private static string StorePath
        {
            get
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                        "vape-store.json");
            }
        }

        public static void EnsureLoaded()
        {
            lock (VapeStore.gate)
            {
                if (VapeStore.data == null)
                {
                    VapeStore.data = VapeStore.Read();
                }
            }
        }

        private static StoreData Read()
        {
            try
            {
                if (File.Exists(VapeStore.StorePath))
                {
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    StoreData loaded = serializer.Deserialize<StoreData>(
                            File.ReadAllText(VapeStore.StorePath));
                    if (loaded != null && loaded.profiles != null)
                    {
                        return loaded;
                    }
                }
            }
            catch (Exception)
            {
            }
            return VapeStore.NewStore();
        }

        private static StoreData NewStore()
        {
            StoreData fresh = new StoreData();
            fresh.profiles = new List<Profile>();
            fresh.friendships = new List<Friendship>();
            fresh.requests = new List<FriendRequestRecord>();
            fresh.configs = new List<PublicConfig>();
            fresh.activeProfile = 0L;
            fresh.settings = new Dictionary<string, string>();
            fresh.privateProfiles = new Dictionary<string, string>();
            return fresh;
        }

        private static void SaveLocked()
        {
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                File.WriteAllText(VapeStore.StorePath, serializer.Serialize(VapeStore.data));
            }
            catch (Exception)
            {
            }
        }

        public static Profile GetLocalProfile()
        {
            lock (VapeStore.gate)
            {
                if (VapeStore.data.activeProfile != 0L)
                {
                    foreach (Profile profile in VapeStore.data.profiles)
                    {
                        if (profile.id == VapeStore.data.activeProfile)
                        {
                            return profile;
                        }
                    }
                }
                if (VapeStore.data.profiles.Count == 0)
                {
                    return null;
                }
                return VapeStore.data.profiles[VapeStore.data.profiles.Count - 1];
            }
        }

        public static void SetActiveProfile(long profileId)
        {
            lock (VapeStore.gate)
            {
                VapeStore.data.activeProfile = profileId;
                VapeStore.SaveLocked();
            }
        }

        public static void SetMinecraftUser(long profileId, string minecraftUsername)
        {
            lock (VapeStore.gate)
            {
                foreach (Profile profile in VapeStore.data.profiles)
                {
                    if (profile.id == profileId)
                    {
                        profile.minecraftUsername = minecraftUsername;
                        VapeStore.SaveLocked();
                        return;
                    }
                }
            }
        }

        public static void RenameProfile(long profileId, string newUsername)
        {
            lock (VapeStore.gate)
            {
                foreach (Profile profile in VapeStore.data.profiles)
                {
                    if (profile.id == profileId)
                    {
                        profile.username = newUsername;
                        VapeStore.SaveLocked();
                        return;
                    }
                }
            }
        }

        public static Profile Login(string username, string password)
        {
            lock (VapeStore.gate)
            {
                foreach (Profile profile in VapeStore.data.profiles)
                {
                    if (string.Equals(profile.username, username,
                            StringComparison.OrdinalIgnoreCase)
                            && profile.password != null
                            && profile.password == password)
                    {
                        VapeStore.data.activeProfile = profile.id;
                        VapeStore.SaveLocked();
                        return profile;
                    }
                }
                return null;
            }
        }

        public static Profile FindProfileByUsername(string username)
        {
            lock (VapeStore.gate)
            {
                foreach (Profile profile in VapeStore.data.profiles)
                {
                    if (string.Equals(profile.username, username,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return profile;
                    }
                }
                return null;
            }
        }

        public static Profile FindProfileById(long id)
        {
            lock (VapeStore.gate)
            {
                foreach (Profile profile in VapeStore.data.profiles)
                {
                    if (profile.id == id)
                    {
                        return profile;
                    }
                }
                return null;
            }
        }

        public static Profile CreateProfile(string username)
        {
            return VapeStore.CreateProfile(username, null);
        }

        public static Profile CreateProfile(string username, string password)
        {
            lock (VapeStore.gate)
            {
                foreach (Profile existing in VapeStore.data.profiles)
                {
                    if (string.Equals(existing.username, username,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }
                }
                Profile profile = new Profile();
                long max = 0L;
                foreach (Profile existing in VapeStore.data.profiles)
                {
                    if (existing.id > max)
                    {
                        max = existing.id;
                    }
                }
                profile.id = max + 1L;
                profile.username = username;
                profile.password = password;
                profile.createdAt = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff") + "+00:00";
                VapeStore.data.profiles.Add(profile);
                VapeStore.data.activeProfile = profile.id;
                VapeStore.SaveLocked();
                return profile;
            }
        }

        public static bool AreFriends(long first, long second)
        {
            lock (VapeStore.gate)
            {
                foreach (Friendship friendship in VapeStore.data.friendships)
                {
                    if ((friendship.a == first && friendship.b == second)
                            || (friendship.a == second && friendship.b == first))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public static List<Profile> GetFriends(long userId)
        {
            List<Profile> friends = new List<Profile>();
            lock (VapeStore.gate)
            {
                foreach (Friendship friendship in VapeStore.data.friendships)
                {
                    long otherId = friendship.a == userId ? friendship.b : friendship.a;
                    if (friendship.a != userId && friendship.b != userId)
                    {
                        continue;
                    }
                    foreach (Profile profile in VapeStore.data.profiles)
                    {
                        if (profile.id == otherId)
                        {
                            friends.Add(profile);
                        }
                    }
                }
            }
            return friends;
        }

        public static void AddFriendship(long first, long second)
        {
            lock (VapeStore.gate)
            {
                if (!VapeStore.AreFriendsLocked(first, second))
                {
                    Friendship friendship = new Friendship();
                    friendship.a = first;
                    friendship.b = second;
                    VapeStore.data.friendships.Add(friendship);
                    VapeStore.SaveLocked();
                }
            }
        }

        private static bool AreFriendsLocked(long first, long second)
        {
            foreach (Friendship friendship in VapeStore.data.friendships)
            {
                if ((friendship.a == first && friendship.b == second)
                        || (friendship.a == second && friendship.b == first))
                {
                    return true;
                }
            }
            return false;
        }

        public static void RemoveFriendship(long first, long second)
        {
            lock (VapeStore.gate)
            {
                for (int index = VapeStore.data.friendships.Count - 1; index >= 0; --index)
                {
                    Friendship friendship = VapeStore.data.friendships[index];
                    if ((friendship.a == first && friendship.b == second)
                            || (friendship.a == second && friendship.b == first))
                    {
                        VapeStore.data.friendships.RemoveAt(index);
                    }
                }
                VapeStore.SaveLocked();
            }
        }

        public static FriendRequestRecord FindRequest(long requester, long recipient)
        {
            lock (VapeStore.gate)
            {
                foreach (FriendRequestRecord request in VapeStore.data.requests)
                {
                    if (request.requester == requester && request.recipient == recipient)
                    {
                        return request;
                    }
                }
                return null;
            }
        }

        public static FriendRequestRecord FindRequestById(long requestId)
        {
            lock (VapeStore.gate)
            {
                foreach (FriendRequestRecord request in VapeStore.data.requests)
                {
                    if (request.id == requestId)
                    {
                        return request;
                    }
                }
                return null;
            }
        }

        public static List<FriendRequestRecord> GetIncomingRequests(long userId)
        {
            List<FriendRequestRecord> incoming = new List<FriendRequestRecord>();
            lock (VapeStore.gate)
            {
                foreach (FriendRequestRecord request in VapeStore.data.requests)
                {
                    if (request.recipient == userId)
                    {
                        incoming.Add(request);
                    }
                }
            }
            return incoming;
        }

        public static List<FriendRequestRecord> GetOutgoingRequests(long userId)
        {
            List<FriendRequestRecord> outgoing = new List<FriendRequestRecord>();
            lock (VapeStore.gate)
            {
                foreach (FriendRequestRecord request in VapeStore.data.requests)
                {
                    if (request.requester == userId)
                    {
                        outgoing.Add(request);
                    }
                }
            }
            return outgoing;
        }

        public static FriendRequestRecord CreateRequest(long requester, long recipient)
        {
            lock (VapeStore.gate)
            {
                long max = 0L;
                foreach (FriendRequestRecord request in VapeStore.data.requests)
                {
                    if (request.id > max)
                    {
                        max = request.id;
                    }
                }
                FriendRequestRecord record = new FriendRequestRecord();
                record.id = max + 1L;
                record.requester = requester;
                record.recipient = recipient;
                VapeStore.data.requests.Add(record);
                VapeStore.SaveLocked();
                return record;
            }
        }

        public static void RemoveRequest(long requestId)
        {
            lock (VapeStore.gate)
            {
                for (int index = VapeStore.data.requests.Count - 1; index >= 0; --index)
                {
                    if (VapeStore.data.requests[index].id == requestId)
                    {
                        VapeStore.data.requests.RemoveAt(index);
                    }
                }
                VapeStore.SaveLocked();
            }
        }

        public static List<PublicConfig> ListConfigs()
        {
            lock (VapeStore.gate)
            {
                return new List<PublicConfig>(VapeStore.data.configs);
            }
        }

        public static PublicConfig GetConfig(long configId)
        {
            lock (VapeStore.gate)
            {
                foreach (PublicConfig config in VapeStore.data.configs)
                {
                    if (config.id == configId)
                    {
                        return config;
                    }
                }
                return null;
            }
        }

        public static PublicConfig CreateConfig(string name, string description,
                List<string> tags, string ownerName, long ownerId, string dataJson)
        {
            lock (VapeStore.gate)
            {
                long max = 0L;
                foreach (PublicConfig config in VapeStore.data.configs)
                {
                    if (config.id > max)
                    {
                        max = config.id;
                    }
                }
                PublicConfig created = new PublicConfig();
                created.id = max + 1L;
                created.ownerId = ownerId;
                created.ownerName = ownerName;
                created.name = name;
                created.description = description == null ? "" : description;
                created.tags = tags == null ? new List<string>() : tags;
                created.data = dataJson == null ? "{}" : dataJson;
                created.downloads = 0L;
                created.likes = 0L;
                created.version = 1L;
                created.createdAt = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff") + "+00:00";
                VapeStore.data.configs.Add(created);
                VapeStore.SaveLocked();
                return created;
            }
        }

        public static void SaveSettings(string scope, string payloadJson)
        {
            lock (VapeStore.gate)
            {
                VapeStore.data.settings[scope] = payloadJson ?? "{}";
                VapeStore.SaveLocked();
            }
        }

        public static string GetSettings(string scope)
        {
            lock (VapeStore.gate)
            {
                string payload;
                return VapeStore.data.settings.TryGetValue(scope, out payload)
                        ? payload : null;
            }
        }

        public static void SavePrivateProfile(string profileId, string payloadJson)
        {
            lock (VapeStore.gate)
            {
                VapeStore.data.privateProfiles[profileId] = payloadJson ?? "{}";
                VapeStore.SaveLocked();
            }
        }

        public static Dictionary<string, string> ListPrivateProfiles()
        {
            lock (VapeStore.gate)
            {
                return new Dictionary<string, string>(VapeStore.data.privateProfiles);
            }
        }

        public static void IncrementDownloads(long configId)
        {
            lock (VapeStore.gate)
            {
                foreach (PublicConfig config in VapeStore.data.configs)
                {
                    if (config.id == configId)
                    {
                        config.downloads++;
                        VapeStore.SaveLocked();
                        return;
                    }
                }
            }
        }
    }
}
