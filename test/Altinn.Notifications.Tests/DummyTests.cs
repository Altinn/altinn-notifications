using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
