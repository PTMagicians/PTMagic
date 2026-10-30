            IServiceProvider logProvider = ServiceHelper.BuildLoggerService(PTMagicBasePath);
            Log = logProvider.GetRequiredService<LogHelper>();

            bool exitLoop = false;

            while (!exitLoop)
            {
                try
                {
                    // Try to read the current runtime summary, but may be being written, so retry if necessary.
                    Summary = JsonConvert.DeserializeObject<Summary>(System.IO.File.ReadAllText(PTMagicBasePath + Constants.PTMagicPathData + Path.DirectorySeparatorChar + "LastRuntimeSummary.json"));
                    exitLoop = true;
                }
                catch (IOException)
                {
                    // Squash exception and try again, as file was locked.
                    Thread.Sleep(250);
                }
            }

            if (Summary.CurrentGlobalSetting == null) Summary.CurrentGlobalSetting = PTMagicConfiguration.AnalyzerSettings.GlobalSettings.Find(s => s.SettingName.IndexOf("default", StringComparison.InvariantCultureIgnoreCase) > -1);

            MainFiatCurrencySymbol = SystemHelper.GetCurrencySymbol(Summary.MainFiatCurrency);

            try
            {