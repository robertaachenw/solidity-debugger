using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Meadow.Shared;

namespace Meadow.Plugin
{
    public class PluginServices
    {
        private readonly Dictionary<string, BenchmarkContext> _benchmarks = new Dictionary<string, BenchmarkContext>();

        private class BenchmarkContext
        {
            public string Name;
            public Stopwatch Stopwatch;
            public long Count;

            public BenchmarkContext(string name)
            {
                Name = name;
                Stopwatch = new Stopwatch();
                Count = 0;
            }

            public long Ticks => Stopwatch.ElapsedTicks;
            public long Milliseconds => Stopwatch.ElapsedMilliseconds;
            
            public void Start()
            {
                Count += 1;
                if (!Stopwatch.IsRunning)
                {
                    Stopwatch.Start();
                }
            }

            public void Stop()
            {
                if (Stopwatch.IsRunning)
                {
                    Stopwatch.Stop();
                }
            }
        }

        public void StartBenchmark(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            
            if (!_benchmarks.ContainsKey(name))
            {
                _benchmarks[name] = new BenchmarkContext(name);
            }

            _benchmarks[name].Start();
        }

        public void StartBenchmark()
        {
            StartBenchmark($"{new StackTrace().GetFrame(1)?.GetMethod()?.Name}");
        }

        public void StopBenchmark()
        {
            StopBenchmark($"{new StackTrace().GetFrame(1)?.GetMethod()?.Name}");
        }
        
        public void StopBenchmark(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            
            if (!_benchmarks.ContainsKey(name))
            {
                return;
            }

            _benchmarks[name].Stop();
        }

        public void PrintBenchmarks()
        {
            foreach (var name in _benchmarks.Keys)
            {
                StopBenchmark(name);
            }
            
            var totalTicks = _benchmarks.Values.Sum(e => e.Ticks);
            if (0 == totalTicks)
            {
                return;
            }

            var lines = _benchmarks.Values
                .OrderBy(e => e.Ticks)
                .Select(e => $"Benchmark: {e.Milliseconds}ms {e.Ticks * 100 / totalTicks:D2}% Count={e.Count} Name={e.Name}")
                .ToArray();

            foreach (var line in lines)
            {
                Log(line);
            }
        }

        private string GetLogFilePath()
        {
            return string.IsNullOrEmpty(Shared.Globals.ProjectContractsDir) ? string.Empty : Path.Combine(Shared.Globals.ProjectContractsDir, "../sdbg.log");
        }
        
        public void Log(string msg)
        {
            var line = $"{DateTime.Now:T} {msg}\n";
            
            Globals.MessageLog.Add(line);

            if (!Globals.IsDebuggerAttached)
            {
                while (Globals.MessageLog.Count > 0)
                {
                    Console.Write(Globals.MessageLog.Take());
                }
            }
        }

        public void HexDump(byte[] arr, int startAddress = 0)
        {
            const int BYTES_PER_LINE = 16;

            for (var i = 0; i < arr.Length; i += BYTES_PER_LINE)
            {
                var line = $"{startAddress + i:X8} ";

                for (var j = 0; j < BYTES_PER_LINE; ++j)
                {
                    if (arr.Length <= i + j)
                    {
                        line += "   ";
                    }
                    else
                    {
                        line += $"{arr[i + j]:X2} ";
                    }
                }

                for (var j = 0; j < BYTES_PER_LINE; ++j)
                {
                    if (arr.Length <= i + j)
                    {
                        line += " ";
                    }
                    else
                    {
                        var c = (char)arr[i + j];
                        if (char.IsControl(c) || char.IsWhiteSpace(c))
                        {
                            line += ".";
                        }
                        else
                        {
                            line += c;
                        }
                    }
                }
                
                Log(line);
            }
        }
    }
}