using System;
using System.Data.SqlClient;
using NServiceBus;
using NServiceBus.Connector.SqlServer;
using NServiceBus.Router;
using Shared;

namespace Sender
{
	class Program
	{
		/// <summary>
		/// Run docker instance:
		///		docker run -d --hostname my-rabbit --name some-rabbit -p 15672:15672 -p 5672:5672 rabbitmq:3-management
		/// </summary>
		/// <param name="args"></param>
		static void Main(string[] args)
		{
			string connectionString = @"Data Source=.\MYDEV;Initial Catalog=update_and_publish;Integrated Security=True;Max Pool Size=100";
			var storage = new SqlSubscriptionStorage(() => new SqlConnection(connectionString), "WebApplication", new SqlDialect.MsSqlServer(), null);
			storage.Install().GetAwaiter().GetResult();

			//The system uses RabbitMQ transport
			var connectorConfig = new ConnectorConfiguration<RabbitMQTransport>(
				name: "WebApplication",
				sqlConnectionString: connectionString,
				customizeConnectedTransport: extensions =>
				{
					extensions.ConnectionString("host=localhost;username=guest;password=guest");
					extensions.UseConventionalRoutingTopology();
				});
			connectorConfig.AutoCreateQueues();

			//Configure where to send messages
			connectorConfig.RouteToEndpoint(
				messageType: typeof(MyMessage),
				endpointName: "Samples.ASPNETCore.Endpoint");

			//Start the connector
			var connector = connectorConfig.CreateConnector();
			connector.Start().GetAwaiter().GetResult();

			while(true)
			{
				var key = Console.ReadKey();
				Console.WriteLine();

				if (key.Key == ConsoleKey.Q)
				{
					break;
				}

				using (var sqlConn = new SqlConnection(connectionString))
				{
					sqlConn.Open();
					using (var tran = sqlConn.BeginTransaction())
					{
						var message = connector.GetSession(sqlConn, tran);
						
						message.Publish(new MyMessage {Message = $"Test {key}"}).ConfigureAwait(false);

						tran.Commit();
					}

					sqlConn.Close();
				}
			}

			Console.ReadLine();
		}
	}
}
