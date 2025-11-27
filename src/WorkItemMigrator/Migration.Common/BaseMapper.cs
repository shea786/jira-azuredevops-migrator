using Migration.Common.Log;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Migration.Common
{
    public class BaseMapper<TRevision> where TRevision : ISourceRevision
    {
        protected Dictionary<string, string> UserMapping { get; private set; }
        
        // Static collection to track all unmapped users across all mapper instances (no duplicates)
        private static readonly HashSet<string> _unmappedUsersInUserMap = new HashSet<string>();

        public BaseMapper(string userMappingPath)
        {
            UserMapping = UserMapper.ParseUserMappings(userMappingPath);
        }

        protected virtual string MapUser(string sourceUser)
        {
            if (sourceUser == null)
                return sourceUser;

            if (UserMapping.TryGetValue(sourceUser, out string wiUser))
            {
                return wiUser;
            }
            else if (UserMapping.TryGetValue("*", out string defaultUser))
            {
                Logger.Log(LogLevel.Warning, $"Could not find user '{sourceUser}' identity in user map. Using default identity '{defaultUser}'.");
                _unmappedUsersInUserMap.Add(sourceUser);
                return defaultUser;
            }
            else
            {
                Logger.Log(LogLevel.Warning, $"Could not find user '{sourceUser}' identity in user map. Using original identity '{sourceUser}'.");
                UserMapping.Add(sourceUser, sourceUser);
                _unmappedUsersInUserMap.Add(sourceUser);
                return sourceUser;
            }
        }
        
        /// <summary>
        /// Writes all unmapped users to a text file, one per line, with no duplicates.
        /// </summary>
        /// <param name="filePath">Path to the output file</param>
        public static void WriteUnmappedUsersToFile(string filePath)
        {
            if (_unmappedUsersInUserMap.Count == 0)
                return;

            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var sortedUsers = _unmappedUsersInUserMap.OrderBy(u => u).ToList();
                File.WriteAllLines(filePath, sortedUsers);
                Logger.Log(LogLevel.Info, $"Wrote {sortedUsers.Count} unmapped user(s) to '{filePath}'");
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warning, $"Failed to write unmapped users to file '{filePath}': {ex.Message}");
            }
        }
        
        /// <summary>
        /// Clears the unmapped users collection (useful for testing or fresh runs)
        /// </summary>
        public static void ClearUnmappedUsers()
        {
            _unmappedUsersInUserMap.Clear();
        }

        protected FieldMapping<TRevision> MergeMapping(params FieldMapping<TRevision>[] mappings)
        {
            var merged = new FieldMapping<TRevision>();
            foreach (var mapping in mappings)
            {
                foreach (var m in mapping)
                    if (!merged.ContainsKey(m.Key))
                        merged[m.Key] = m.Value;
            }
            return merged;
        }

        protected string Crop(string value, int maxSize)
        {
            var max = Math.Min(value.Length, maxSize);
            return value.Substring(0, max);
        }
    }
}
