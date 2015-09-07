using System;
﻿using OmniSharp.Models.v1;
using Microsoft.Framework.ConfigurationModel;

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
