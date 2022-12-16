using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;

using TestClient.Facilities;

namespace TestClient
{
	class Program
	{
		#region InnerTypes
		enum RunTypes
		{
			Unknown,
			Interactive,
			Batched,
			TimeLimited,
			Help,
			PduParser,
		}

		enum ClientTypes
		{
			Unknown,
			Client,
			Communicator
		}
		#endregion

		static ConcurrentBag<string> SentMessages = new ConcurrentBag<string>();

		private static readonly global::Common.Logging.ILog _log = null;
		private static readonly Stopwatch _sw;

		static Program()
		{
			SetupLogging();
			_log = global::Common.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
			_sw = System.Diagnostics.Stopwatch.StartNew();
		}

		private static void SetupLogging()
		{
			NLog.LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(
				Path.Combine(
					Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
					"NLog.config"
				)
			);

			Common.Logging.LogManager.Adapter =
				new Common.Logging.NLog45.NLogLoggerFactoryAdapter(
					new Common.Logging.Configuration.NameValueCollection()
					{
						{ "configType", "EXTERNAL" }
					});
		}

		static T GetArg<T>(string[] args, int index, T defaultValue, Func<string, object> converter = null) where T : IConvertible
		{
			converter = converter ?? new Func<string, object>((string arg) => Convert.ChangeType(arg, typeof(T)));
			try
			{
				return args.Length <= index ? defaultValue : (T)converter(args[index]);
			}
			catch
			{
				return defaultValue;
			}
		}

		static void PrintHelp()
		{
			Console.WriteLine("{0} <action> <clientType> <clients> <workers> <requests|timeLimitSeconds> <enableTls>",
					Process.GetCurrentProcess().ProcessName);
			Console.WriteLine("action: type of test\n\t{0}", string.Join("|", Enum.GetNames(typeof(RunTypes))));
			Console.WriteLine("clientType: client version\n\t{0}", string.Join("|", Enum.GetNames(typeof(ClientTypes))));
			Console.WriteLine("clients: number of client version");
			Console.WriteLine("workers: number of client workers");
			Console.WriteLine("requests: number of client requests (for Batched tests)");
			Console.WriteLine("timeLimitSeconds: time to execute the test and count request send (for TimeLimited tests)");
		}

		static void Main(string[] args)
		{
			var action = GetArg(args, index: 0, defaultValue: RunTypes.Interactive, (arg) => Enum.Parse(typeof(RunTypes), arg, true));

			if (action == RunTypes.Help)
			{
				PrintHelp();
				return;
			}

			string text1 = "+593979122175";
			string text2 = "+59XXXXXXXXXX";

			Console.WriteLine("{0}=>{1}", text1, BitConverter.ToString(System.Text.Encoding.ASCII.GetBytes(text1)).Replace("-",""));
			Console.WriteLine("{0}=>{1}", text2, BitConverter.ToString(System.Text.Encoding.ASCII.GetBytes(text2)).Replace("-", ""));

			if (action == RunTypes.PduParser)
			{
				var parser = new PduParser();
				var command = parser.Parse(args[1]);
				_log.Info(command);
				return;
			}


			
			var clientType = GetArg(args, index: 1, defaultValue: ClientTypes.Client, (arg) => Enum.Parse(typeof(ClientTypes), arg, true));

			int clients = GetArg(args, index: 2, defaultValue: 1);
			int workers = GetArg(args, index: 3, defaultValue: 1);
			int requests = GetArg(args, index: 4, defaultValue: 500);
			int timeLimitSeconds = GetArg(args, index: 4, defaultValue: 10);

			bool enableTls = GetArg(args, index: 5, defaultValue: false, (arg) => string.Compare("enableTls", arg, true) == 0);

			ISmppClientFactory factory = clientType == ClientTypes.Communicator ? new SMPPCommunicatorFactory() : new SMPPClientFactory();

			switch (action)
			{
				case RunTypes.Interactive:
					new InteractiveTest(factory, enableTls).Run(clients: 1, requests: 0, workers: 1);
					break;
				case RunTypes.Batched:
					new BatchedTests(factory, enableTls).Run(clients, workers, requests);
					break;
				case RunTypes.TimeLimited:
					new TimeLimitedTests(factory, enableTls).TimeLimitedRun(clients, workers, TimeSpan.FromSeconds(timeLimitSeconds));
					break;
				default:
					break;
			}
		}
	}
}
