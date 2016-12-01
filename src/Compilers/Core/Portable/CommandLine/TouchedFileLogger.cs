// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Roslyn.Utilities;
using System;
using System.Linq;
using System.IO;
using System.Threading;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// Used for logging all the paths which are "touched" (used) in any way
    /// in the process of compilation. It can also save files for purpose of repro'ing.
    /// </summary>
    internal class TouchedFileLogger
    {
        private string _touchedFilesPath;
        private ConcurrentSet<string> _readFiles;
        private ConcurrentSet<string> _writtenFiles;
        private string _reproFolderPath;
        private TextWriter _consoleOutput;
        private Func<string, TextWriter, FileMode, FileAccess, FileShare, Stream> _tryOpenFile;
        private Action<string, string, TextWriter> _tryCopyFile;

        // Environment variable to enable logging a repro and control which compilations should trigger such logging.
        private static string reproTriggerEnvVariable = "RoslynCommandLineLogRepro";

        // Environment variable to determine the folder to log to.
        private static string logEnvVariable = "RoslynCommandLineLogFile";

        private TouchedFileLogger(
            string touchedFilePath,
            string reproFolderPath,
            TextWriter consoleOutput,
            Func<string, TextWriter, FileMode, FileAccess, FileShare, Stream> tryOpenFile,
            Action<string, string, TextWriter> tryCopyFile)
        {
            _touchedFilesPath = touchedFilePath;
            if (touchedFilePath != null)
            {
                _readFiles = new ConcurrentSet<string>();
                _writtenFiles = new ConcurrentSet<string>();
            }
            else
            {
                _readFiles = null;
                _writtenFiles = null;
            }

            _reproFolderPath = reproFolderPath;
            _consoleOutput = consoleOutput;
            _tryOpenFile = tryOpenFile;
            _tryCopyFile = tryCopyFile;
        }

        public static TouchedFileLogger CreateIfNeeded(
            CommandLineArguments arguments,
            TextWriter consoleOutput,
            Func<string, TextWriter, FileMode, FileAccess, FileShare, Stream> tryOpenFile,
            Action<string, string, TextWriter> tryCopyFile,
            Func<string, TextWriter, string> tryGetEnvVar)
        {
            var logFolder = IsReproLogEnabled(arguments, tryGetEnvVar, consoleOutput);
            string reproFolder = GetReproFolder(logFolder, arguments);

            if (logFolder != null)
            {
                try
                {
                    if (Directory.Exists(reproFolder))
                    {
                        Directory.Delete(reproFolder, recursive: true);
                    }
                }
                catch (Exception e)
                {
                    // TODO
                    consoleOutput.WriteLine(e.Message);
                }
            }

            if (arguments.TouchedFilesPath != null || logFolder != null)
            {
                return new TouchedFileLogger(arguments.TouchedFilesPath, reproFolder, consoleOutput, tryOpenFile, tryCopyFile);
            }
            return null;
        }

        private static string GetReproFolder(string logFolder, CommandLineArguments arguments)
        {
            if (logFolder == null)
            {
                return null;
            }
            return $"{Path.Combine(logFolder, (arguments.OutputFileName ?? "repro"))}-{Guid.NewGuid().ToString()}";
        }

        public static string IsReproLogEnabled(CommandLineArguments arguments, Func<string, TextWriter, string> tryGetEnvVar, TextWriter consoleOutput)
        {
            string reproCondition = tryGetEnvVar(reproTriggerEnvVariable, consoleOutput);
            if (reproCondition == null)
            {
                return null;
            }

            if (!arguments.SourcePaths.Any(f => Path.GetFileName(f) == reproCondition))
            {
                return null;
            }

            string loggingFileName = tryGetEnvVar(logEnvVariable, consoleOutput);
            if (loggingFileName != null)
            {
                if (Directory.Exists(loggingFileName))
                {
                    return loggingFileName;
                }
                else
                {
                    return Path.GetDirectoryName(loggingFileName);
                }
            }

            return null;
        }

        public void AddRead(string path)
        {
            if (path == null) throw new ArgumentNullException(path);
            if (_readFiles != null)
            {
                _readFiles.Add(path);
            }
        }

        public void AddReadSource(string path)
        {
            AddRead("src", path);
        }

        public void AddReadReference(string path)
        {
            AddRead("ref", path);
        }

        private void AddRead(string kind, string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(path);
            }
            if (_readFiles != null)
            {
                _readFiles.Add(path);
            }
            if (_reproFolderPath != null)
            {
                Copy(kind, path);
            }
        }

        private void Copy(string kind, string path)
        {
            string filename = Path.GetFileName(path);
            string dir = Path.Combine(_reproFolderPath, kind);
            Directory.CreateDirectory(dir);

            string dest = EnsureNoConflicts(Path.Combine(dir, filename));
            _tryCopyFile(path, dest, _consoleOutput);
        }

        /// <summary>
        /// If the destination file already exists, insert a counter into the path, to avoid conflicts.
        /// For instance, `/repro/src/duplicate.cs` could become `/repro/src/duplicate.2.cs`
        /// </summary>
        private string EnsureNoConflicts(string origDest)
        {
            int counter = 2;
            string dest = origDest;
            while (File.Exists(dest))
            {
                string extension = Path.GetExtension(origDest);
                dest = Path.ChangeExtension(origDest, $"{counter}.{extension}");
                counter++;
            }

            return dest;
        }

        public void AddWritten(string path)
        {
            if (path == null) throw new ArgumentNullException(path);
            if (_writtenFiles != null) _writtenFiles.Add(path);
        }

        /// <summary>
        /// Writes all of the paths the TouchedFileLogger to the given
        /// TextWriter in upper case. After calling this method the
        /// logger is in an undefined state.
        /// </summary>
        private void WriteReadPaths(TextWriter s)
        {
            var temp = new string[_readFiles.Count];
            int i = 0;
            var readFiles = Interlocked.Exchange(
                ref _readFiles,
                null);
            foreach (var path in readFiles)
            {
                temp[i] = path.ToUpperInvariant();
                i++;
            }
            Array.Sort<string>(temp);

            foreach (var path in temp)
            {
                s.WriteLine(path);
            }
        }

        /// <summary>
        /// Writes all of the paths the TouchedFileLogger to the given
        /// TextWriter in upper case. After calling this method the
        /// logger is in an undefined state.
        /// </summary>
        private void WriteWrittenPaths(TextWriter s)
        {
            var temp = new string[_writtenFiles.Count];
            int i = 0;
            var writtenFiles = Interlocked.Exchange(
                ref _writtenFiles,
                null);
            foreach (var path in writtenFiles)
            {
                temp[i] = path.ToUpperInvariant();
                i++;
            }
            Array.Sort<string>(temp);

            foreach (var path in temp)
            {
                s.WriteLine(path);
            }
        }

        public bool Finish()
        {
            if (_touchedFilesPath == null)
            {
                return true;
            }

            var readStream = _tryOpenFile(_touchedFilesPath + ".read", _consoleOutput,
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            if (readStream == null)
            {
                return false;
            }

            using (var writer = new StreamWriter(readStream))
            {
                this.WriteReadPaths(writer);
            }

            var writtenStream = _tryOpenFile(_touchedFilesPath + ".write", _consoleOutput,
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            if (writtenStream == null)
            {
                return false;
            }

            using (var writer = new StreamWriter(writtenStream))
            {
                this.WriteWrittenPaths(writer);
            }

            return true;
        }
    }
}