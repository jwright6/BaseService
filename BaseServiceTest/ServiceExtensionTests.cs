using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BaseService;

namespace BaseServiceTest
{
    [TestClass]
    public class ServiceExtensionTests
    {
        [TestMethod]
        public void Each_EnumerableNull()
        {
            ((IEnumerable<object>)null).Each()
        }
    }
}
