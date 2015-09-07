using System;
using Microsoft.Framework.ConfigurationModel;
﻿using OmniSharp.Models.v1;

namespace OmniSharp.Services
{
    public interface IProjectSystem
    {
        string Key { get; }
        void Initalize(IConfiguration configuration);
        object GetInformationModel(ProjectInformationRequest request);
        object GetProjectModel(string path);
    }
}
