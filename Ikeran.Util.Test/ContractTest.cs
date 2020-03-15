using System;
using System.Linq.Expressions;
using AgileObjects.ReadableExpressions;
using NUnit.Framework;

namespace Ikeran.Util.Test
{
    [TestFixture]
    public class ContractTest
    {
        [Test]
        public void Lambda()
        {
            int a = 12;
            int b = 18;
            ContractException e = null;
            try
            {
                Contract.Assert(() => a > b);
                Assert.Fail("expected exception not thrown");
            }
            catch (ContractException ex)
            {
                e = ex;
            }
            Assert.That(e.Message, Contains.Substring("12 > 18"));
        }
    }
}
