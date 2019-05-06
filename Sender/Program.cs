using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
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
		///
		/// Dummy SQL table:
		///  CREATE TABLE [dbo].[TestTable](
		///  [Id] [int] IDENTITY(1,1) NOT NULL,
		///  [Name] [varchar](10) NOT NULL,
		///  CONSTRAINT [PK_TestTable] PRIMARY KEY CLUSTERED 
		///  (
		///  [Id] ASC
		///  )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY])  ON [PRIMARY]
		/// GO
		/// </summary>
		/// <param name="args"></param>
		static void Main(string[] args)
        {
            Start().GetAwaiter().GetResult();
        }

        static async Task Start()
        {
            Console.Title = "Sender";
            string connectionString = @"Data Source=(local);Initial Catalog=test3;Integrated Security=True;Max Pool Size=100";
            var storage = new SqlSubscriptionStorage(() => new SqlConnection(connectionString), "WebApplication",
                new SqlDialect.MsSqlServer(), null);
            storage.Install().GetAwaiter().GetResult();

            //The system uses RabbitMQ transport
            var connectorConfig = new ConnectorConfiguration<RabbitMQTransport>(
                name: "WebApplication",
                sqlConnectionString: connectionString,
                customizeConnectedTransport: extensions =>
                {
                    extensions.ConnectionString("host=localhost;username=guest;password=guest");
                    extensions.UseConventionalRoutingTopology();
                },
                customizeConnectedInterface: configuration => { configuration.EnableMessageDrivenPublishSubscribe(storage); });
            connectorConfig.AutoCreateQueues();

            //Configure where to send messages
            connectorConfig.RouteToEndpoint(
                messageType: typeof(MyMessage),
                endpointName: "Samples.ASPNETCore.Endpoint");

            //Start the connector
            var connector = connectorConfig.CreateConnector();
            await connector.Start();

            Console.WriteLine("(Q) Quit (R) Rollback (Any other key) Write to TestTable and publish message");

            while (true)
            {
                var key = Console.ReadKey();
                Console.WriteLine();

                if (key.Key == ConsoleKey.Q)
                {
                    break;
                }

                using (var sqlConn = new SqlConnection(connectionString))
                {
                    await sqlConn.OpenAsync();
                    using (var tran = sqlConn.BeginTransaction())
                    {
                        var message = connector.GetSession(sqlConn, tran);

                        await message.Publish(new MyMessage {Message = $"Test {key}"}).ConfigureAwait(false);

                        using (var command = new SqlCommand("INSERT INTO TestTable VALUES ('Test')", sqlConn, tran))
                        {
                            await command.ExecuteNonQueryAsync();
                        }

                        if (key.Key == ConsoleKey.R)
                        {
                            tran.Rollback(); // Simulate rollback
                        }
                        else
                        {
                            tran.Commit(); // Business as usual
                        }
                    }
                }
            }

            await connector.Stop();
            Console.ReadLine();
        }
    }
}
