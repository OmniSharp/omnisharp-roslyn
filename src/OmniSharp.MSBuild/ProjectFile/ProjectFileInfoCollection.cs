using System;
using System.Collections;
using System.Collections.Generic;

namespace OmniSharp.MSBuild.ProjectFile
{
    public class ProjectFileInfoCollection : IEnumerable<ProjectFileInfo>
    {
        private readonly List<ProjectFileInfo> _items;
        private readonly Dictionary<string, ProjectFileInfo> _itemMap;

        public ProjectFileInfoCollection()
        {
            _items = new List<ProjectFileInfo>();
            _itemMap = new Dictionary<string, ProjectFileInfo>(StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerator<ProjectFileInfo> GetEnumerator()
        {
            foreach (var item in _items)
            {
                yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(ProjectFileInfo fileInfo)
        {
            if (fileInfo == null)
            {
                throw new ArgumentNullException(nameof(fileInfo));
            }

            if (_itemMap.ContainsKey(fileInfo.FilePath))
            {
                throw new ArgumentException($"Project file already exists: {fileInfo.FilePath}", nameof(fileInfo));
            }

            _items.Add(fileInfo);
            _itemMap.Add(fileInfo.FilePath, fileInfo);
        }

        public bool ContainsKey(string filePath)
        {
            return _itemMap.ContainsKey(filePath);
        }

        public bool TryGetValue(string filePath, out ProjectFileInfo fileInfo)
        {
            return _itemMap.TryGetValue(filePath, out fileInfo);
        }

        public ProjectFileInfo this[string filePath]
        {
            get
            {
                return _itemMap[filePath];
            }
            set
            {
                ProjectFileInfo oldFileInfo;
                if (_itemMap.TryGetValue(filePath, out oldFileInfo))
                {
                    var index = _items.IndexOf(oldFileInfo);

                    _items[index] = value;
                    _itemMap[filePath] = value;
                }
                else
                {
                    Add(value);
                }
            }
        }
    }
}