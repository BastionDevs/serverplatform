//Many thanks to Nikita Lyadov.
//View the Original project at https://github.com/niklyadov/tiny-ini-file-class

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace TinyINIController
{
    internal class IniFile
    {
        private readonly string _exe = Assembly.GetExecutingAssembly().GetName().Name;

        private readonly FileAccess _fileAccess;

        private readonly FileInfo _fileInfo;

        public IniFile(string path = null, FileAccess access = FileAccess.ReadWrite)
        {
            _fileAccess = access;
            _fileInfo = new FileInfo(path ?? _exe);
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern long WritePrivateProfileString(string section, string key, string value, string filePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(string section, string key, string @default,
            StringBuilder retVal, int size, string filePath);

        public string Read(string key, string section = null)
        {
            var retVal = new StringBuilder(65025);

            if (_fileAccess != FileAccess.Write)
                GetPrivateProfileString(section ?? _exe, key, "", retVal, 65025, _fileInfo.FullName);
            else
                throw new Exception("Can`t read file! No access!");

            return retVal.ToString();
        }

        public void Write(string key, string value, string section = null)
        {
            if (_fileAccess != FileAccess.Read)
                WritePrivateProfileString(section ?? _exe, key, value, _fileInfo.FullName);
            else
                throw new Exception("Can`t write to file! No access!");
        }

        public void DeleteKey(string key, string section = null)
        {
            Write(key, null, section ?? _exe);
        }

        public void DeleteSection(string section = null)
        {
            Write(null, null, section ?? _exe);
        }

        public bool KeyExists(string key, string section = null)
        {
            return Read(key, section).Length > 0;
        }
    }
}