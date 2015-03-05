using System.Collections.Generic;
using Microsoft.AspNet.Mvc;
using OmniSharp.Models;
using OmniSharp.Services;

namespace OmniSharp
{
    public class FilesController
    {
        private readonly IFileSystemWatcher _watcher;

        public FilesController(IFileSystemWatcher watcher)
        {
            _watcher = watcher;
        }

        [HttpPost("/filesChanged")]
        public bool OnFilesChanged(IEnumerable<Request> requests)
        {
            foreach (var request in requests)
            {
                _watcher.TriggerChange(request.FileName);
            }
            return true;
        }
    }
}