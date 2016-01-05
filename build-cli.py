#!/usr/bin/env python

import os
import os.path
import platform
import shutil
import sys
import tarfile
from subprocess import call
from subprocess import check_call
from glob import glob

print "Build OmniSharp-Roslyn with dotnet \n"

working_dir = os.path.dirname(os.path.abspath(sys.argv[0]))
print "Working Dir: ", working_dir

output_dir =  os.path.join(working_dir, 'artifacts', 'cli')
print "Output dir: ", output_dir

project_name = 'OmniSharp.CliHost'

if '-h' in sys.argv:
    print "Print help"
    exit(0)

if call("dotnet > /dev/null", shell=True) != 0:
    print "dotnet is missing"
    exit(1)

# restore packages
if not '--skip-restore' in sys.argv:
    restore_cmd = 'dotnet restore'
    if platform.system() == 'Darwin':
        restore_cmd += ' --runtime osx.10.10-x64'
    check_call(restore_cmd, shell=True)

# publish
if not '--skip-build' in sys.argv:
    # clean previous build
    if os.path.exists(output_dir):
        shutil.rmtree(output_dir)
    project_dir = os.path.join(working_dir, 'src', project_name)
    publish_cmd = 'dotnet publish --output {0} --framework dnxcore50'.format(output_dir)
    check_call(publish_cmd, shell=True, cwd=project_dir)

# zip
if not '--skip-package' in sys.argv:
    print "Packaging the OmniSharp.CliHost"

    tar = tarfile.open(os.path.join(working_dir, 'artifacts', "omnisharp-cli.tar"), mode='w:gz')
    tar.add(output_dir, "omnisharp-cli/" + os.name)
    tar.close()

    exit(0)
