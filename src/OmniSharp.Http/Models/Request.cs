﻿using System.IO;

namespace OmniSharp.Models
{
    public class Request
    {
        private string _fileName;

        public int Line { get; set; }
        public int Column { get; set; }
        public string Buffer { get; set; }
        public string FileName
        {
            get
            {
                return _fileName.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }
            set
            {
                _fileName = value;
           } 
        }
    }
}
