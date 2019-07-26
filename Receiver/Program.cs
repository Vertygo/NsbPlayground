using System;
using System.Threading.Tasks;
using NServiceBus;
using Shared;

namespace Receiver
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.Title = "Receiver";
            Start().GetAwaiter().GetResult();
		}

        static async Task Start()
        {
            var config = new EndpointConfiguration("WebApplication");
            var transport = config.UseTransport<RabbitMQTransport>();
            transport.ConnectionString("host=localhost");
            transport.UseConventionalRoutingTopology();

            var persistence = config.UsePersistence<InMemoryPersistence>();

            var endpoint = await Endpoint.Start(config);

            Console.WriteLine("Press <enter> to exit");
            Console.ReadLine();

            await endpoint.Stop();
        }
	}

    class MyMessageHandler : IHandleMessages<MyMessage>
    {
        public Task Handle(MyMessage message, IMessageHandlerContext context)
        {
            Console.WriteLine($"MyMessage handled {message.Message}");
            return Task.CompletedTask;
        }
    }
}
