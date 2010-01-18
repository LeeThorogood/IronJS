﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Dynamic;

namespace IronJS.Runtime.Js
{
    using Et = System.Linq.Expressions.Expression;
    using Meta = System.Dynamic.DynamicMetaObject;

    public class Lambda
    {
        public Func<IScope, IObj, object> Delegate { get; protected set; }
        public List<string> Params { get; protected set; }

        public Lambda(Func<IScope, IObj, object> func, string[] parms)
            : this(func, parms.ToList())
        {

        }

        public Lambda(Func<IScope, IObj, object> func)
            : this(func, new List<string>())
        {

        }

        public Lambda(Func<IScope, IObj, object> func, List<string> parms)
        {
            Delegate = func;
            Params = parms;
        }
    }
}
