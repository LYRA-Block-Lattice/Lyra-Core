/* 
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System;

namespace Clifton.Blockchain
{
    public class MerkleException : Exception
    {
        public MerkleException(string msg) : base(msg)
        {
        }
    }
}
