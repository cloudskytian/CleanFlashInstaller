﻿namespace CleanFlashCommon
{
    public class ExitedProcess
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }

        public bool IsSuccessful => ExitCode == 0;
    }
}
