﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;
using System.Net;
using System.Threading.Tasks;


//
// This app takes one stock symbol, downloads historial data, and does some
// simple analysis in parallel:
//
//    min price
//    max price
//    avg price
//    standard deviation
//    standard error
//
// This version uses 3 web sites (yahoo, nasdaq, and msn) for redundancy, and
// downloads the data in parallel --- the data displayed is from the first 
// site that returns.
//
// Usage:  StockHistory.exe  msft
//

namespace StockHistory
{
	class Program
	{

		static void Main(string[] args)
		{
			String version, platform, symbol;
			int numYearsOfHistory;

			ProcessCmdLineArgs(args, out version, out platform, out symbol, out numYearsOfHistory);

			//
			// Process stock symbol:
			//
			ProcessStockSymbol(symbol, numYearsOfHistory);

			Console.WriteLine();
			Console.WriteLine("** Done **");
			Console.WriteLine();

			Console.Write("\n\nPress a key to exit...");
			Console.ReadKey();
		}


		/// <summary>
		/// Downloads and processes historical data for given stock symbol.
		/// </summary>
		/// <param name="symbol">stock symbol, e.g. "msft"</param>
		/// <param name="numYearsOfHistory">years of history > 0, e.g. 10</param>
		private static void ProcessStockSymbol(string symbol, int numYearsOfHistory)
		{
		    try
		    {
		        StockData data = DownloadData.GetHistoricalData(symbol, numYearsOfHistory);

		        int N = data.Prices.Count;

		        Task<decimal> T_min = Task.Factory.StartNew<decimal>(() =>
		            {
		                return data.Prices.Min();
		            }
		        );

		        Task<decimal> T_max = Task.Factory.StartNew<decimal>(() =>
		            {
		                return data.Prices.Max();
		            }
		        );

		        Task<decimal> T_avg = Task.Factory.StartNew<decimal>(() =>
		            {
		                return data.Prices.Average();
		            }
		        );

		        Task<double> T_stddev = Task.Factory.StartNew(() =>
		            {
		                // Standard deviation:
		                double sum = 0.0;
		                decimal l_avg = data.Prices.Average();

		                foreach (decimal value in data.Prices)
		                    sum += Math.Pow(Convert.ToDouble(value - l_avg), 2.0);

		                //
		                // NOTE: want to test exception handling?  Uncomment the following to trigger
		                // divide-by-zero, and notice you get 2 task exceptions, one from this code
		                // and one from the continuation task (stderr) that fails because we fail.
		                //
		                //int abc = 0;
		                //int x = 1000 / abc;

		                return Math.Sqrt(sum/N);
		            }
		        );

		        Task<double> T_stderr = T_stddev.ContinueWith((antecedent) =>
		            {
		                return antecedent.Result/Math.Sqrt(N);
		            }
		        );

		        //
		        // Wait and harvest results when done:
		        //
		        // NOTE: even though WaitAll is not required for correctness (calls to .Result do
		        // an implicit .Wait), we use WaitAll for efficiency so that we process tasks in
		        // order as they finish (versus an arbitrary order implied by calls to .Result).
		        //
		        Task.WaitAll(new Task[] {T_min, T_max, T_avg, T_stddev, T_stderr});

		        decimal min = T_min.Result;
		        decimal max = T_max.Result;
		        decimal avg = T_avg.Result;
		        double stddev = T_stddev.Result;
		        double stderr = T_stderr.Result;

		        //
		        // Output:
		        //
		        Console.WriteLine();
		        Console.WriteLine("** {0} **", symbol);
		        Console.WriteLine("   Data source:  '{0}'", data.DataSource);
		        Console.WriteLine("   Data points:   {0:#,##0}", N);
		        Console.WriteLine("   Min price:    {0:C}", min);
		        Console.WriteLine("   Max price:    {0:C}", max);
		        Console.WriteLine("   Avg price:    {0:C}", avg);
		        Console.WriteLine("   Std dev/err:   {0:0.000} / {1:0.000}", stddev, stderr);
		    }
		    catch (AggregateException ae)
		    {
		        Console.WriteLine();
                ae = ae.Flatten();

		        foreach (var ex in ae.InnerExceptions)
		        {
		            Console.WriteLine(ex);
		        }
		    }
			catch (Exception ex)
			{
				Console.WriteLine();
				Console.WriteLine("** {0} **", symbol);
				Console.WriteLine("Error: {0}", ex.Message);
			}
		}


		/// <summary>
		/// Processes command-line arguments, and outputs to the user.
		/// </summary>
		/// 
		static void ProcessCmdLineArgs(string[] args, out string version, out string platform, out string symbol, out int numYearsOfHistory)
		{
#if DEBUG
			version = "debug";
#else
			version = "release";
#endif

#if _WIN64
	platform = "64-bit";
#elif _WIN32
	platform = "32-bit";
#else
			platform = "any-cpu";
#endif

			symbol = "";  // in case user does not supply:
			numYearsOfHistory = 10;

			string usage = "Usage: StockHistory.exe [-? /? symbol ]";

			if (args.Length > 1)
			{
				Console.WriteLine("** Error: incorrect number of arguments (found {0}, expecting 1)", args.Length);
				Console.WriteLine(usage);
				System.Environment.Exit(-1);
			}

			for (int i = 0; i < args.Length; i++)
			{
				string arg = args[i];

				if (arg == "-?" || arg == "/?")
				{
					Console.WriteLine(usage);
					System.Environment.Exit(-1);
				}
				else  // assume arg is stock symbol:
				{
					symbol = arg;
				}
			}//for

			if (symbol == "")
			{
				Console.WriteLine();
				Console.Write("Please enter stock symbol (e.g. 'msft'): ");
				symbol = Console.ReadLine();
			}

			symbol = symbol.Trim();  // delete any leading/trailing spaces:
			if (symbol == "")
			{
				Console.WriteLine();
				Console.WriteLine("** Error: you must enter a stock symbol, e.g. 'msft'");
				Console.WriteLine(usage);
				Console.WriteLine();
				System.Environment.Exit(-1);
			}

			Console.WriteLine();
			Console.WriteLine("** Parallel Stock History App [{0}, {1}] **", platform, version);
			Console.WriteLine("   Stock symbol:     {0}", symbol);
			Console.WriteLine("   Time period:      last {0} years", numYearsOfHistory);
			Console.WriteLine("   Internet access?  {0}", DownloadData.IsConnectedToInternet());
		}

	}//class
}//namespace
