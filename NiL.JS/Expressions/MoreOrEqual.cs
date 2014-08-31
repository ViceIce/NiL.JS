﻿using System;
using NiL.JS.Core;

namespace NiL.JS.Expressions
{
    [Serializable]
    public sealed class MoreOrEqual : Less
    {
        public MoreOrEqual(CodeNode first, CodeNode second)
            : base(first, second)
        {

        }

        internal override JSObject Evaluate(Context context)
        {
            return base.Evaluate(context).iValue == 0;
        }

        public override string ToString()
        {
            return "(" + first + " >= " + second + ")";
        }
    }
}