using System;
namespace Ikeran.Util
{
    public class InvalidDataException : InvalidOperationException
    {
        public InvalidDataException()
        {
        }

        public InvalidDataException(string message) : base(message)
        {
        }
    }
}
