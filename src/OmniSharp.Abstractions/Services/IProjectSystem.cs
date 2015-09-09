using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Framework.ConfigurationModel;
﻿using OmniSharp.Models.v1;

namespace OmniSharp.Services
{
    public interface IProjectSystem
    {
        string Key { get; }
        string Language { get; }
        IEnumerable<string> Extensions { get; }
        void Initalize(IConfiguration configuration);
        Task<object> GetInformationModel(ProjectInformationRequest request);
        Task<object> GetProjectModel(string path);
    }
}
