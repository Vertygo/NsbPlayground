using System;
using NServiceBus;

namespace Shared
{
	public class MyMessage : IEvent
	{
		public string Message { get; set; }
	}
}
