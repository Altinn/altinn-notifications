using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Persistence;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Altinn.Notifications.Tests
{
    public  class DummyTests
    {
        [Fact]
        public void InitialTest()
        {
            string actual = "Stephanie er kul";

            Assert.Equal("Stephanie er kul", actual);
        }
    }
}
