using System;

﻿namespace OmniSharp.Services
{
    public interface IProjectSystem
    {
        void Initalize();
        object GetInformation();
        object GetProject(string path);
    }
}
