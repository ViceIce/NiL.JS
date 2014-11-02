using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NiL.JS.Core.Modules;
using NiL.JS.Core.TypeProxing;

namespace NiL.JS.Core.BaseTypes
{
    [Serializable]
    public sealed class Array : JSObject
    {
        private static IEnumerable<KeyValuePair<long, string>> iterablyEnum(long length, JSObject src)
        {
            var res = new List<KeyValuePair<long, string>>();
            var @enum = src.GetEnumerator(false);
            while (@enum.MoveNext())
            {
                var i = @enum.Current;
                var pindex = 0;
                var dindex = 0.0;
                long lindex = 0;
                if (Tools.ParseNumber(i, ref pindex, out dindex)
                    && (pindex == i.Length)
                    && dindex < length
                    && (lindex = (long)dindex) == dindex)
                {
                    res.Add(new KeyValuePair<long, string>(lindex, i));
                }
            }
            res.Sort(new Comparison<KeyValuePair<long, string>>((x, y) => System.Math.Sign(x.Key - y.Key)));
            return res;
        }

        private static long getLengthOfIterably(JSObject src, bool reassignLen)
        {
            var len = src.GetMember("length", true, false); // ��� �� �������� �� null/undefined � �������� ���� ����
            if (len.valueType == JSObjectType.Property)
            {
                if (reassignLen && (len.attributes & JSObjectAttributesInternal.ReadOnly) == 0)
                {
                    len.valueType = JSObjectType.Undefined;
                    len.Assign(((len.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(src, null));
                }
                else
                    len = ((len.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(src, null);
            }
            uint res;
            if (len.valueType >= JSObjectType.Object)
                res = (uint)Tools.JSObjectToInt64(len.ToPrimitiveValue_Value_String(), 0, false);
            else
                res = (uint)Tools.JSObjectToInt64(len, 0, false);
            if (reassignLen)
            {
                if (len.valueType == JSObjectType.Property)
                    ((len.oValue as PropertyPair).set ?? Function.emptyFunction).Invoke(src, new Arguments() { a0 = res, length = 1 });
                else
                    len.Assign(res);
            }
            return res;
        }

        private static Array iterableToArray(JSObject src, bool evalProps, bool clone, bool reassignLen, long _length = -1)
        {
            Array temp = new Array();
            HashSet<string> processedKeys = null;
            bool goDeep = true;
            for (; goDeep; )
            {
                goDeep = false;
                if (src.GetType() == typeof(Array))
                {
                    if (_length == -1)
                        _length = (src as Array).data.Length;
                    long prew = -1;
                    foreach (var element in ((src as Array).data as IEnumerable<KeyValuePair<int, JSObject>>))
                    {
                        if (element.Key >= _length) // ���...
                            break;
                        var value = element.Value;
                        if (value == null || !value.isExist)
                            continue;
                        if (!goDeep && System.Math.Abs(prew - element.Key) > 1)
                            goDeep = true;
                        if (evalProps && value.valueType == JSObjectType.Property)
                            value = (value.oValue as PropertyPair).get == null ? undefined : (value.oValue as PropertyPair).get.Invoke(src, null).CloneImpl();
                        else if (clone)
                            value = value.CloneImpl();
                        if (processedKeys != null)
                        {
                            var sk = element.Key.ToString();
                            if (processedKeys.Contains(sk))
                                continue;
                            processedKeys.Add(sk);
                        }
                        temp.data[element.Key] = value;
                    }
                    goDeep |= System.Math.Abs(prew - _length) > 1;
                }
                else
                {
                    if (_length == -1)
                    {
                        _length = getLengthOfIterably(src, reassignLen);
                        if (_length == 0)
                            return temp;
                    }
                    long prew = -1;
                    var tjo = new JSObject() { valueType = JSObjectType.String };
                    foreach (var index in iterablyEnum(_length, src))
                    {
                        tjo.oValue = index.Value;
                        var value = src.GetMember(tjo, false, false);
                        if (!value.isExist)
                            continue;
                        if (evalProps && value.valueType == JSObjectType.Property)
                            value = (value.oValue as PropertyPair).get == null ? undefined : (value.oValue as PropertyPair).get.Invoke(src, null).CloneImpl();
                        else if (clone)
                            value = value.CloneImpl();
                        if (!goDeep && System.Math.Abs(prew - index.Key) > 1)
                            goDeep = true;
                        if (processedKeys != null)
                        {
                            var sk = index.Value;
                            if (processedKeys.Contains(sk))
                                continue;
                            processedKeys.Add(sk);
                        }
                        temp.data[(int)(uint)index.Key] = value;
                    }
                    goDeep |= System.Math.Abs(prew - _length) > 1;
                }
                var crnt = src;
                if (src.__proto__ == Null)
                    break;
                src = src.__proto__.oValue as JSObject ?? src.__proto__;
                if (src == null || (src.valueType >= JSObjectType.String && src.oValue == null))
                    break;
                if (processedKeys == null)
                {
                    processedKeys = new HashSet<string>();
                    for (var @enum = crnt.GetEnumeratorImpl(false); @enum.MoveNext(); )
                        processedKeys.Add(@enum.Current);
                }
            }
            temp.data[(int)(_length - 1)] = temp.data[(int)(_length - 1)];
            return temp;
        }

        private sealed class _lengthField : JSObject
        {
            private Array array;

            public _lengthField(Array owner)
            {
                attributes |= JSObjectAttributesInternal.DoNotDelete | JSObjectAttributesInternal.DoNotEnum | JSObjectAttributesInternal.NotConfigurable | JSObjectAttributesInternal.Reassign;
                array = owner;
                if ((long)(int)array.data.Length == array.data.Length)
                {
                    this.iValue = (int)array.data.Length;
                    this.valueType = JSObjectType.Int;
                }
                else
                {
                    this.dValue = array.data.Length;
                    this.valueType = JSObjectType.Double;
                }
            }

            public override void Assign(JSObject value)
            {
                var nlenD = Tools.JSObjectToDouble(value);
                var nlen = (uint)nlenD;
                if (double.IsNaN(nlenD) || double.IsInfinity(nlenD) || nlen != nlenD)
                    throw new JSException(new RangeError("Invalid array length"));
                if ((attributes & JSObjectAttributesInternal.ReadOnly) != 0)
                    return;
                array.setLength(nlen);
                if ((long)(int)array.data.Length == array.data.Length)
                {
                    this.iValue = (int)array.data.Length;
                    this.valueType = JSObjectType.Int;
                }
                else
                {
                    this.dValue = array.data.Length;
                    this.valueType = JSObjectType.Double;
                }
            }
        }

        private static readonly SparseArray<JSObject> emptyData = new SparseArray<JSObject>();
        [Hidden]
        internal SparseArray<JSObject> data;

        [DoNotEnumerate]
        public Array()
        {
            oValue = this;
            valueType = JSObjectType.Object;
            data = new SparseArray<JSObject>();
            attributes |= JSObjectAttributesInternal.SystemObject;
            _lengthObj = new _lengthField(this);
        }

        [DoNotEnumerate]
        public Array(int length)
        {
            oValue = this;
            valueType = JSObjectType.Object;
            if (length < 0)
                throw new JSException((new RangeError("Invalid array length.")));
            data = new SparseArray<JSObject>();
            if (length > 0)
                data[length - 1] = null;
            attributes |= JSObjectAttributesInternal.SystemObject;
            _lengthObj = new _lengthField(this);
        }

        [CLSCompliant(false)]
        [Hidden]
        public Array(long length)
        {
            oValue = this;
            valueType = JSObjectType.Object;
            if (length < 0 || (uint)length > uint.MaxValue)
                throw new JSException((new RangeError("Invalid array length.")));
            data = new SparseArray<JSObject>((int)length);
            attributes |= JSObjectAttributesInternal.SystemObject;
        }

        [DoNotEnumerate]
        public Array(double d)
        {
            oValue = this;
            valueType = JSObjectType.Object;
            if (((long)d != d) || (d < 0) || (d > 0xffffffff))
                throw new JSException((new RangeError("Invalid array length.")));
            data = new SparseArray<JSObject>();
            if (d > 0)
                data[(int)((uint)d - 1)] = null;
            attributes |= JSObjectAttributesInternal.SystemObject;
        }

        [DoNotEnumerate]
        public Array(Arguments args)
            : this(MethodProxy.argumentsToArray(args))
        {

        }

        [Hidden]
        public Array(ICollection collection)
        {
            oValue = this;
            valueType = JSObjectType.Object;
            if (collection == null)
                throw new ArgumentNullException("collection");
            data = new SparseArray<JSObject>();
            if (collection.Count > 0)
                data[(int)collection.Count - 1] = null;
            var index = 0;
            foreach (var e in collection)
                data[index++] = e is JSObject ? (e as JSObject).CloneImpl() : TypeProxy.Proxy(e);
            attributes |= JSObjectAttributesInternal.SystemObject;
        }

        [Hidden]
        public Array(IEnumerable enumerable)
        {
            oValue = this;
            valueType = JSObjectType.Object;
            if (enumerable == null)
                throw new ArgumentNullException("enumerable");
            data = new SparseArray<JSObject>();
            var index = 0;
            foreach (var e in enumerable)
                data[index++] = e is JSObject ? (e as JSObject).CloneImpl() : TypeProxy.Proxy(e);
            attributes |= JSObjectAttributesInternal.SystemObject;
        }

        [Hidden]
        internal Array(IEnumerator enumerator)
        {
            oValue = this;
            valueType = JSObjectType.Object;
            if (enumerator == null)
                throw new ArgumentNullException("enumerator");
            data = new SparseArray<JSObject>();
            var index = 0;
            while (enumerator.MoveNext())
            {
                var e = enumerator.Current;
                data[index++] = e is JSObject ? (e as JSObject).CloneImpl() : TypeProxy.Proxy(e);
            }
            attributes |= JSObjectAttributesInternal.SystemObject;
        }

        [Hidden]
        public override void Assign(JSObject value)
        {
            if ((attributes & JSObjectAttributesInternal.ReadOnly) == 0)
                throw new InvalidOperationException("Try to assign to array");
        }

        [Hidden]
        public void Add(JSObject obj)
        {
            data.Add(obj);
        }

        private _lengthField _lengthObj;
        [Hidden]
        public JSObject length
        {
            [Hidden]
            get
            {
                if (_lengthObj == null)
                    _lengthObj = new _lengthField(this);
                if (data.Length <= int.MaxValue)
                {
                    _lengthObj.iValue = (int)data.Length;
                    _lengthObj.valueType = JSObjectType.Int;
                }
                else
                {
                    _lengthObj.dValue = data.Length > uint.MaxValue ? 0 : (uint)data.Length;
                    _lengthObj.valueType = JSObjectType.Double;
                }
                return _lengthObj;
            }
        }

        internal bool setLength(long nlen)
        {
            if (data.Length == nlen)
                return true;
            if (nlen < 0)
                throw new JSException(new RangeError("Invalid array length"));
            if (data.Length > nlen)
            {
                var res = true;
                foreach (var element in data.Reversed)
                {
                    if ((uint)element.Key < nlen)
                        break;
                    if (element.Value != null
                        && element.Value.isExist
                        && (element.Value.attributes & JSObjectAttributesInternal.DoNotDelete) != 0)
                    {
                        nlen = element.Key;
                        res = false;
                    }
                }
                if (!res)
                {
                    setLength(nlen + 1); // ����������� �������� �� ����� ����.
                    return false;
                }
            }
            if (data.Length > nlen) do
                {
                    data.RemoveAt((int)(data.Length - 1));
                    data.Trim();
                }
                while (data.Length > nlen);
            if (data.Length != nlen)
                data[(int)nlen - 1] = data[(int)nlen - 1];
            return true;
        }

        [DoNotEnumerate]
        [AllowUnsafeCall(typeof(JSObject))]
        public JSObject concat(Arguments args)
        {
            Array res = null;
            var lenObj = this.GetMember("length", true);
            if (!lenObj.isDefinded)
            {
                JSObject _this = this;
                if (_this.valueType < JSObjectType.Object)
                {
                    _this = new JSObject()
                    {
                        __proto__ = this.__proto__,
                        valueType = JSObjectType.Object,
                        oValue = this
                    };
                }
                res = new Array() { _this };
            }
            else
                res = iterableToArray(this, true, true, false);
            for (var i = 0; i < args.length; i++)
            {
                var v = args[i];
                var varr = v.oValue as Array;
                if (varr != null)
                {
                    varr = iterableToArray(varr, true, false, false);
                    for (var ai = 0; ai < varr.data.Length; ai++)
                    {
                        var item = varr.data[ai];
                        res.data.Add(item);
                    }
                }
                else
                {
                    res.data.Add(v.CloneImpl());
                }
            }
            return res;
        }

        [DoNotEnumerate]
        [AllowUnsafeCall(typeof(JSObject))]
        public JSObject every(Arguments args)
        {
            bool nativeMode = this.GetType() == typeof(Array);
            Array src = this;
            if (!src.isDefinded || (src.valueType >= JSObjectType.Object && src.oValue == null))
                throw new JSException(new TypeError("Can not call Array.prototype.every for null or undefined"));
            if (!nativeMode)
                src = iterableToArray(src, false, false, false);
            Function f = args[0] == null ? null : args[0].oValue as Function;
            if (f == null)
                throw new JSException(new TypeError("Callback argument is not a function."));
            var ao = new Arguments();
            ao.length = 3;
            ao[0] = new JSObject();
            ao[1] = new JSObject();
            ao[2] = this;
            if (this.valueType < JSObjectType.Object)
            {
                ao[2] = new JSObject() { valueType = JSObjectType.Object, __proto__ = this.__proto__, fields = src.fields };
                ao[2].oValue = this;
            }
            var tb = args.Length > 1 ? args[1] : null;
            var context = Context.CurrentContext;
            IEnumerator<KeyValuePair<int, JSObject>> alternativeEnum = null;
            long prew = -1;
            var _length = (src as Array).data.Length;
            var mainEnum = ((src as Array).data as IEnumerable<KeyValuePair<int, JSObject>>).GetEnumerator();
            bool moved = true;
            while (moved)
            {
                moved = mainEnum.MoveNext();
                while (moved && (mainEnum.Current.Value == null || !mainEnum.Current.Value.isExist))
                    moved = mainEnum.MoveNext();
                var element = mainEnum.Current;
                uint key = (uint)element.Key;
                if (!moved && (mainEnum.Current.Value == null || !mainEnum.Current.Value.isExist))
                    key = _length;
                bool alter = false;
                var value = element.Value;
                if (key - prew > 1 || (!moved && key < _length))
                {
                    if (alternativeEnum == null)
                    {
                        alternativeEnum = (iterableToArray(this.__proto__, false, false, false, _length).data as IEnumerable<KeyValuePair<int, JSObject>>).GetEnumerator();
                        alternativeEnum.MoveNext();
                    }
                    alter = true;
                }
                do
                {
                    if (alter)
                    {
                        bool amoved = alternativeEnum.Current.Key > prew || alternativeEnum.MoveNext();
                        while ((uint)alternativeEnum.Current.Key <= prew && amoved)
                            amoved = alternativeEnum.MoveNext();
                        if (amoved && (uint)alternativeEnum.Current.Key > prew
                                   && ((uint)alternativeEnum.Current.Key < (uint)element.Key
                                        || !moved && (uint)alternativeEnum.Current.Key < _length))
                        {
                            key = (uint)alternativeEnum.Current.Key;
                            value = alternativeEnum.Current.Value;
                        }
                        else
                        {
                            alter = false;
                            value = element.Value;
                            key = (uint)element.Key;
                        }
                    }
                    if (key >= _length
                        || (!alter && !moved))
                        break;
                    prew = key;
                    if (value == null || !value.isExist)
                        continue;
                    if (value.valueType == JSObjectType.Property)
                        value = (value.oValue as PropertyPair).get == null ? undefined : (value.oValue as PropertyPair).get.Invoke(this, null);
                    ao[0].Assign(value);
                    ao[1].Assign(context.wrap(key));
                    if (!(bool)f.Invoke(tb, ao))
                        return false;
                }
                while (alter);
            }
            return true;
        }

        [DoNotEnumerate]
        [AllowUnsafeCall(typeof(JSObject))]
        public JSObject some(Arguments args)
        {
            bool nativeMode = this.GetType() == typeof(Array);
            Array src = this;
            if (!src.isDefinded || (src.valueType >= JSObjectType.Object && src.oValue == null))
                throw new JSException(new TypeError("Can not call Array.prototype.some for null or undefined"));
            if (!nativeMode)
                src = iterableToArray(src, false, false, false);
            Function f = args[0] == null ? null : args[0].oValue as Function;
            if (f == null)
                throw new JSException(new TypeError("Callback argument is not a function."));
            var ao = new Arguments();
            ao.length = 3;
            ao[0] = new JSObject();
            ao[1] = new JSObject();
            ao[2] = this;
            if (this.valueType < JSObjectType.Object)
            {
                ao[2] = new JSObject() { valueType = JSObjectType.Object, __proto__ = this.__proto__, fields = src.fields };
                ao[2].oValue = this;
            }
            var tb = args.Length > 1 ? args[1] : null;
            var context = Context.CurrentContext;
            IEnumerator<KeyValuePair<int, JSObject>> alternativeEnum = null;
            long prew = -1;
            var _length = (src as Array).data.Length;
            var mainEnum = ((src as Array).data as IEnumerable<KeyValuePair<int, JSObject>>).GetEnumerator();
            bool moved = true;
            while (moved)
            {
                moved = mainEnum.MoveNext();
                while (moved && (mainEnum.Current.Value == null || !mainEnum.Current.Value.isExist))
                    moved = mainEnum.MoveNext();
                var element = mainEnum.Current;
                uint key = (uint)element.Key;
                if (!moved && (mainEnum.Current.Value == null || !mainEnum.Current.Value.isExist))
                    key = _length;
                bool alter = false;
                var value = element.Value;
                if (key - prew > 1 || (!moved && key < _length))
                {
                    if (alternativeEnum == null)
                    {
                        alternativeEnum = (iterableToArray(this.__proto__, false, false, false, _length).data as IEnumerable<KeyValuePair<int, JSObject>>).GetEnumerator();
                        alternativeEnum.MoveNext();
                    }
                    alter = true;
                }
                do
                {
                    if (alter)
                    {
                        bool amoved = alternativeEnum.Current.Key > prew || alternativeEnum.MoveNext();
                        while ((uint)alternativeEnum.Current.Key <= prew && amoved)
                            amoved = alternativeEnum.MoveNext();
                        if (amoved && (uint)alternativeEnum.Current.Key > prew
                                   && ((uint)alternativeEnum.Current.Key < (uint)element.Key
                                        || !moved && (uint)alternativeEnum.Current.Key < _length))
                        {
                            key = (uint)alternativeEnum.Current.Key;
                            value = alternativeEnum.Current.Value;
                        }
                        else
                        {
                            alter = false;
                            value = element.Value;
                            key = (uint)element.Key;
                        }
                    }
                    if (key >= _length
                        || (!alter && !moved))
                        break;
                    prew = key;
                    if (value == null || !value.isExist)
                        continue;
                    if (value.valueType == JSObjectType.Property)
                        value = (value.oValue as PropertyPair).get == null ? undefined : (value.oValue as PropertyPair).get.Invoke(this, null);
                    ao[0].Assign(value);
                    ao[1].Assign(context.wrap(key));
                    if ((bool)f.Invoke(tb, ao))
                        return true;
                }
                while (alter);
            }
            return false;
        }

        [DoNotEnumerate]
        [AllowUnsafeCall(typeof(JSObject))]
        public JSObject filter(Arguments args)
        {
            bool nativeMode = this.GetType() == typeof(Array);
            Array src = this;
            if (!src.isDefinded || (src.valueType >= JSObjectType.Object && src.oValue == null))
                throw new JSException(new TypeError("Can not call Array.prototype.filter for null or undefined"));
            if (!nativeMode)
                src = iterableToArray(src, false, false, false);
            Function f = args[0] == null ? null : args[0].oValue as Function;
            if (f == null)
                throw new JSException(new TypeError("Callback argument is not a function."));
            var ao = new Arguments();
            ao.length = 3;
            ao[0] = new JSObject();
            ao[1] = new JSObject();
            ao[2] = this;
            if (this.valueType < JSObjectType.Object)
            {
                ao[2] = new JSObject() { valueType = JSObjectType.Object, __proto__ = this.__proto__, fields = src.fields };
                ao[2].oValue = this;
            }
            var tb = args.Length > 1 ? args[1] : null;
            var context = Context.CurrentContext;
            IEnumerator<KeyValuePair<int, JSObject>> alternativeEnum = null;
            long prew = -1;
            var _length = (src as Array).data.Length;
            var mainEnum = ((src as Array).data as IEnumerable<KeyValuePair<int, JSObject>>).GetEnumerator();
            var res = new SparseArray<JSObject>();
            bool moved = true;
            while (moved)
            {
                moved = mainEnum.MoveNext();
                while (moved && (mainEnum.Current.Value == null || !mainEnum.Current.Value.isExist))
                    moved = mainEnum.MoveNext();
                var element = mainEnum.Current;
                uint key = (uint)element.Key;
                if (!moved && (mainEnum.Current.Value == null || !mainEnum.Current.Value.isExist))
                    key = _length;
                bool alter = false;
                var value = element.Value;
                if (key - prew > 1 || (!moved && key < _length))
                {
                    if (alternativeEnum == null)
                    {
                        alternativeEnum = (iterableToArray(this.__proto__, false, false, false, _length).data as IEnumerable<KeyValuePair<int, JSObject>>).GetEnumerator();
                        alternativeEnum.MoveNext();
                    }
                    alter = true;
                }
                do
                {
                    if (alter)
                    {
                        bool amoved = alternativeEnum.Current.Key > prew || alternativeEnum.MoveNext();
                        while ((uint)alternativeEnum.Current.Key <= prew && amoved)
                            amoved = alternativeEnum.MoveNext();
                        if (amoved && (uint)alternativeEnum.Current.Key > prew
                                   && ((uint)alternativeEnum.Current.Key < (uint)element.Key
                                        || !moved && (uint)alternativeEnum.Current.Key < _length))
                        {
                            key = (uint)alternativeEnum.Current.Key;
                            value = alternativeEnum.Current.Value;
                        }
                        else
                        {
                            alter = false;
                            value = element.Value;
                            key = (uint)element.Key;
                        }
                    }
                    if (key >= _length
                        || (!alter && !moved))
                        break;
                    prew = key;
                    if (value == null || !value.isExist)
                        continue;
                    if (value.valueType == JSObjectType.Property)
                        value = (value.oValue as PropertyPair).get == null ? undefined : (value.oValue as PropertyPair).get.Invoke(this, null);
                    ao[0].Assign(value);
                    ao[1].Assign(context.wrap(key));
                    if ((bool)f.Invoke(tb, ao))
                        res[(int)(uint)key] = value.CloneImpl();
                }
                while (alter);
            }
            var rres = new Array();
            foreach (var item in res)
                if (item != null)
                    rres.data.Add(item);
            return rres;
        }

        [DoNotEnumerate]
        [AllowUnsafeCall(typeof(JSObject))]
        public JSObject map(Arguments args)
        {
            bool nativeMode = this.GetType() == typeof(Array);
            Array src = this;
            if (!src.isDefinded || (src.valueType >= JSObjectType.Object && src.oValue == null))
                throw new JSException(new TypeError("Can not call Array.prototype.map for null or undefined"));
            if (!nativeMode)
                src = iterableToArray(src, false, false, false);
            Function f = args[0] == null ? null : args[0].oValue as Function;
            if (f == null)
                throw new JSException(new TypeError("Callback argument is not a function."));
            var ao = new Arguments();
            ao.length = 3;
            ao[0] = new JSObject();
            ao[1] = new JSObject();
            ao[2] = this;
            if (this.valueType < JSObjectType.Object)
            {
                ao[2] = new JSObject() { valueType = JSObjectType.Object, __proto__ = this.__proto__, fields = src.fields };
                ao[2].oValue = this;
            }
            var tb = args.Length > 1 ? args[1] : null;
            var context = Context.CurrentContext;
            IEnumerator<KeyValuePair<int, JSObject>> alternativeEnum = null;
            long prew = -1;
            var _length = (src as Array).data.Length;
            var mainEnum = ((src as Array).data as IEnumerable<KeyValuePair<int, JSObject>>).GetEnumerator();
            var res = new Array();
            bool moved = true;
            while (moved)
            {
                moved = mainEnum.MoveNext();
                while (moved && (mainEnum.Current.Value == null || !mainEnum.Current.Value.isExist))
                    moved = mainEnum.MoveNext();
                var element = mainEnum.Current;
                uint key = (uint)element.Key;
                if (!moved && (mainEnum.Current.Value == null || !mainEnum.Current.Value.isExist))
                    key = _length;
                bool alter = false;
                var value = element.Value;
                if (key - prew > 1 || (!moved && key < _length))
                {
                    if (alternativeEnum == null)
                    {
                        alternativeEnum = (iterableToArray(this.__proto__, false, false, false, _length).data as IEnumerable<KeyValuePair<int, JSObject>>).GetEnumerator();
                        alternativeEnum.MoveNext();
                    }
                    alter = true;
                }
                do
                {
                    if (alter)
                    {
                        bool amoved = alternativeEnum.Current.Key > prew || alternativeEnum.MoveNext();
                        while ((uint)alternativeEnum.Current.Key <= prew && amoved)
                            amoved = alternativeEnum.MoveNext();
                        if (amoved && (uint)alternativeEnum.Current.Key > prew
                                   && ((uint)alternativeEnum.Current.Key < (uint)element.Key
                                        || !moved && (uint)alternativeEnum.Current.Key < _length))
                        {
                            key = (uint)alternativeEnum.Current.Key;
                            value = alternativeEnum.Current.Value;
                        }
                        else
                        {
                            alter = false;
                            value = element.Value;
                            key = (uint)element.Key;
                        }
                    }
                    if (key >= _length
                        || (!alter && !moved))
                        break;
                    prew = key;
                    if (value == null || !value.isExist)
                        continue;
                    if (value.valueType == JSObjectType.Property)
                        value = (value.oValue as PropertyPair).get == null ? undefined : (value.oValue as PropertyPair).get.Invoke(this, null);
                    ao[0].Assign(value);
                    ao[1].Assign(context.wrap(key));
                    res.data[(int)key] = f.Invoke(tb, ao).CloneImpl();
                }
                while (alter);
            }
            res.data[(int)_length - 1] = res.data[(int)_length - 1];
            return res;
        }

        [DoNotEnumerate]
        [AllowUnsafeCall(typeof(JSObject))]
        public JSObject forEach(Arguments args)
        {
            bool nativeMode = this.GetType() == typeof(Array);
            Array src = this;
            if (!src.isDefinded || (src.valueType >= JSObjectType.Object && src.oValue == null))
                throw new JSException(new TypeError("Can not call Array.prototype.forEach for null or undefined"));
            if (!nativeMode)
                src = iterableToArray(src, false, false, false);
            Function f = args[0] == null ? null : args[0].oValue as Function;
            if (f == null)
                throw new JSException(new TypeError("Callback argument is not a function."));
            var ao = new Arguments();
            ao.length = 3;
            ao[0] = new JSObject();
            ao[1] = new JSObject();
            ao[2] = this;
            if (this.valueType < JSObjectType.Object)
            {
                ao[2] = new JSObject() { valueType = JSObjectType.Object, __proto__ = this.__proto__, fields = src.fields };
                ao[2].oValue = this;
            }
            var tb = args.Length > 1 ? args[1] : null;
            var context = Context.CurrentContext;
            IEnumerator<KeyValuePair<int, JSObject>> alternativeEnum = null;
            long prew = -1;
            var _length = (src as Array).data.Length;
            var mainEnum = ((src as Array).data as IEnumerable<KeyValuePair<int, JSObject>>).GetEnumerator();
            bool moved = true;
            while (moved)
            {
                moved = mainEnum.MoveNext();
                while (moved && (mainEnum.Current.Value == null || !mainEnum.Current.Value.isExist))
                    moved = mainEnum.MoveNext();
                var element = mainEnum.Current;
                uint key = (uint)element.Key;
                if (!moved && (mainEnum.Current.Value == null || !mainEnum.Current.Value.isExist))
                    key = _length;
                bool alter = false;
                var value = element.Value;
                if (key - prew > 1 || (!moved && key < _length))
                {
                    if (alternativeEnum == null)
                    {
                        alternativeEnum = (iterableToArray(this.__proto__, false, false, false, _length).data as IEnumerable<KeyValuePair<int, JSObject>>).GetEnumerator();
                        alternativeEnum.MoveNext();
                    }
                    alter = true;
                }
                do
                {
                    if (alter)
                    {
                        bool amoved = alternativeEnum.Current.Key > prew || alternativeEnum.MoveNext();
                        while ((uint)alternativeEnum.Current.Key <= prew && amoved)
                            amoved = alternativeEnum.MoveNext();
                        if (amoved && (uint)alternativeEnum.Current.Key > prew
                                   && ((uint)alternativeEnum.Current.Key < (uint)element.Key
                                        || !moved && (uint)alternativeEnum.Current.Key < _length))
                        {
                            key = (uint)alternativeEnum.Current.Key;
                            value = alternativeEnum.Current.Value;
                        }
                        else
                        {
                            alter = false;
                            value = element.Value;
                            key = (uint)element.Key;
                        }
                    }
                    if (key >= _length
                        || (!alter && !moved))
                        break;
                    prew = key;
                    if (value == null || !value.isExist)
                        continue;
                    if (value.valueType == JSObjectType.Property)
                        value = (value.oValue as PropertyPair).get == null ? undefined : (value.oValue as PropertyPair).get.Invoke(this, null);
                    ao[0].Assign(value);
                    ao[1].Assign(context.wrap(key));
                    f.Invoke(tb, ao);
                }
                while (alter);
            }
            return null;
        }

        [DoNotEnumerateAttribute]
        [AllowUnsafeCall(typeof(JSObject))]
        public JSObject indexOf(Arguments args)
        {
            if (args == null)
                throw new ArgumentNullException("args");
            var el = args[0];
            JSObject src = this;
            if (!src.isDefinded || (src.valueType >= JSObjectType.Object && src.oValue == null))
                throw new JSException(new TypeError("Can not call Array.prototype.indexOf for null or undefined"));
            long _length = -1;
            HashSet<string> processedKeys = null;
            for (; ; )
            {
                if (src.GetType() == typeof(Array))
                {
                    if (_length == -1)
                        _length = (src as Array).data.Length;
                    var fromIndex = Tools.JSObjectToInt64(args[1], 0, true);
                    if (fromIndex < 0)
                        fromIndex += _length;
                    var len = _length;
                    foreach (var element in ((src as Array).data as IEnumerable<KeyValuePair<int, JSObject>>))
                    {
                        if (element.Key >= len) // ���...
                            break;
                        var value = element.Value;
                        if (value == null || !value.isExist)
                            continue;
                        if (value.valueType == JSObjectType.Property)
                            value = (value.oValue as PropertyPair).get == null ? undefined : (value.oValue as PropertyPair).get.Invoke(src, null);
                        if (processedKeys != null)
                        {
                            var sk = element.Key.ToString();
                            if (processedKeys.Contains(sk))
                                continue;
                            processedKeys.Add(sk);
                        }
                        if ((uint)element.Key >= fromIndex && Expressions.StrictEqual.Check(value, el, null))
                            return Context.CurrentContext.wrap((uint)element.Key);
                    }
                }
                else
                {
                    if (src.valueType == JSObjectType.String)
                        return (src["indexOf"].oValue as Function).Invoke(src, args);
                    if (_length == -1)
                    {
                        _length = getLengthOfIterably(src, false);
                    }
                    var fromIndex = Tools.JSObjectToInt64(args[1], 0, true);
                    if (fromIndex < 0)
                        fromIndex += _length;
                    var @enum = src.GetEnumeratorImpl(false);
                    while (@enum.MoveNext())
                    {
                        var i = @enum.Current;
                        var pindex = 0;
                        var dindex = 0.0;
                        long lindex = 0;
                        if (Tools.ParseNumber(i, ref pindex, out dindex)
                            && (pindex == i.Length)
                            && dindex < _length
                            && (lindex = (long)dindex) == dindex)
                        {
                            var temp = src[i];
                            if (temp.valueType == JSObjectType.Property)
                                temp = (temp.oValue as PropertyPair).get == null ? undefined : (temp.oValue as PropertyPair).get.Invoke(src, null);
                            if (!temp.isExist)
                                continue;
                            if (processedKeys != null)
                            {
                                var sk = lindex.ToString();
                                if (processedKeys.Contains(sk))
                                    continue;
                                processedKeys.Add(sk);
                            }
                            if (lindex >= fromIndex && Expressions.StrictEqual.Check(temp, el, null))
                            {
                                return lindex;
                            }
                        }
                    }
                }
                var crnt = src;
                if (src.__proto__ == Null)
                    break;
                src = src.__proto__;
                if (src == null || (src.valueType >= JSObjectType.String && src.oValue == null))
                    break;
                if (processedKeys == null)
                    processedKeys = new HashSet<string>(crnt);
            }
            return -1;
        }

        [DoNotEnumerateAttribute]
        public static JSObject isArray(Arguments args)
        {
            if (args == null)
                throw new ArgumentNullException("args");
            return args[0].Value is Array || args[0].Value == TypeProxy.GetPrototype(typeof(Array));
        }

        [DoNotEnumerate]
        [AllowUnsafeCall(typeof(JSObject))]
        public JSObject join(Arguments separator)
        {
            return joinImpl(separator == null || separator.length == 0 || !separator[0].isDefinded ? "," : separator[0].ToString(), false);
        }

        private string joinImpl(string separator, bool locale)
        {
            if ((this.oValue == null && this.valueType >= JSObjectType.Object) || this.valueType <= JSObjectType.Undefined)
                throw new JSException(new TypeError("Array.prototype.join called for null or undefined"));
            if (this.valueType >= JSObjectType.Object && this.oValue.GetType() == typeof(Array))
            {
                if (data.Length == 0)
                    return "";
                var _data = this.data;
                try
                {
                    this.data = emptyData;
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    JSObject t;
                    for (var i = 0L; i < (long)_data.Length; i++)
                    {
                        t = _data[(int)i];
                        if (((t != null && t.isExist) || null != (t = this.GetMember(i.ToString(), false, false)))
                            && t.isDefinded)
                        {
                            if (t.valueType < JSObjectType.String || t.oValue != null)
                                sb.Append(locale ? t.ToPrimitiveValue_LocaleString_Value() : t.ToPrimitiveValue_String_Value());
                        }
                        sb.Append(separator);
                    }
                    sb.Length -= separator.Length;
                    return sb.ToString();
                }
                finally
                {
                    this.data = _data;
                }
            }
            else
            {
                return iterableToArray(this, true, false, false).joinImpl(separator, locale);
            }
        }

        [DoNotEnumerateAttribute]
        [AllowUnsafeCall(typeof(JSObject))]
        public JSObject lastIndexOf(Arguments args)
        {
            if (args == null)
                throw new ArgumentNullException("args");
            var el = args[0];
            JSObject src = this;
            if (!src.isDefinded || (src.valueType >= JSObjectType.Object && src.oValue == null))
                throw new JSException(new TypeError("Can not call Array.prototype.indexOf for null or undefined"));
            HashSet<string> processedKeys = null;
            long _length = -1;
            for (; ; )
            {
                if (src.GetType() == typeof(Array))
                {
                    if (_length == -1)
                    {
                        _length = (src as Array).data.Length;
                        long pos = long.MaxValue;
                        if (args.Length > 1)
                            pos = Tools.JSObjectToInt64(args[1], 0, true);
                        if (pos < 0)
                            _length = pos + _length + 1;
                        else if (pos < _length)
                            _length = pos + 1;
                    }
                    foreach (var element in (src as Array).data.Reversed)
                    {
                        if ((uint)element.Key >= _length
                               || element.Value == null
                               || !element.Value.isExist)
                            continue;
                        var item = element.Value;
                        if (item.valueType == JSObjectType.Property)
                            item = (item.oValue as PropertyPair).get == null ? undefined : (item.oValue as PropertyPair).get.Invoke(src, null);
                        if (processedKeys != null)
                        {
                            var sk = element.Key.ToString();
                            if (processedKeys.Contains(sk))
                                continue;
                            processedKeys.Add(sk);
                        }
                        if (Expressions.StrictEqual.Check(item, el, null))
                            return Context.CurrentContext.wrap((uint)element.Key);
                    }
                }
                else
                {
                    if (src.valueType == JSObjectType.String)
                        return (src["lastIndexOf"].oValue as Function).Invoke(src, args);
                    if (_length == -1)
                    {
                        var len = src["length"]; // ��� �� �������� �� null/undefined � �������� ���� ����
                        if (!len.isDefinded)
                            return -1;
                        if (len.valueType == JSObjectType.Property)
                            len = ((len.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(src, null);
                        if (len.valueType >= JSObjectType.Object)
                            len = len.ToPrimitiveValue_Value_String();
                        _length = (uint)Tools.JSObjectToInt64(len);
                        long pos = long.MaxValue;
                        if (args.Length > 1)
                            pos = Tools.JSObjectToInt64(args[1], 0, true);
                        if (pos < 0)
                            _length = pos + _length + 1;
                        else if (pos < _length)
                            _length = pos + 1;
                    }
                    long result = -1;
                    var @enum = src.GetEnumeratorImpl(false);
                    while (@enum.MoveNext())
                    {
                        var i = @enum.Current;
                        var pindex = 0;
                        var dindex = 0.0;
                        long lindex = 0;
                        if (Tools.ParseNumber(i, ref pindex, out dindex)
                            && (pindex == i.Length)
                            && dindex < _length
                            && (lindex = (long)dindex) == dindex)
                        {
                            if (processedKeys != null)
                            {
                                var sk = lindex.ToString();
                                if (processedKeys.Contains(sk))
                                    continue;
                                processedKeys.Add(sk);
                            }
                            var item = this[i];
                            if (item.valueType == JSObjectType.Property)
                                item = (item.oValue as PropertyPair).get == null ? undefined : (item.oValue as PropertyPair).get.Invoke(src, null);
                            if (item.isExist && Expressions.StrictEqual.Check(item, el, null))
                            {
                                result = lindex;
                            }
                        }
                    }
                    if (result != -1)
                        return result;
                }
                var crnt = src;
                if (src.__proto__ == Null)
                    break;
                src = src.__proto__;
                if (src == null || (src.valueType >= JSObjectType.String && src.oValue == null))
                    break;
                if (processedKeys == null)
                    processedKeys = new HashSet<string>(crnt);
            }
            return -1;
        }

        [DoNotEnumerate]
        [AllowUnsafeCall(typeof(JSObject))]
        public JSObject pop()
        {
            notExists.valueType = JSObjectType.NotExistsInObject;
            if (this.GetType() == typeof(Array))
            {
                if (data.Length == 0)
                    return notExists;
                var res = data[(int)(data.Length - 1)] ?? this[(data.Length - 1).ToString()];
                int newLen = (int)(data.Length - 1);
                data.RemoveAt(newLen);
                data[newLen - 1] = data[newLen - 1];
                if (res.valueType == JSObjectType.Property)
                    res = ((res.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(this, null).CloneImpl();
                return res;
            }
            else
            {
                var length = getLengthOfIterably(this, true);
                if (length <= 0 || length > uint.MaxValue)
                    return notExists;
                length--;
                this["length"] = length;
                var tres = this.GetMember(Context.CurrentContext.wrap(length.ToString()), true, false);
                JSObject res;
                if (tres.valueType == JSObjectType.Property)
                    res = ((tres.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(this, null).CloneImpl();
                else
                    res = tres.CloneImpl();
                if ((tres.attributes & JSObjectAttributesInternal.DoNotDelete) == 0)
                {
                    tres.oValue = null;
                    tres.valueType = JSObjectType.NotExistsInObject;
                }
                return res;
            }
        }

        [DoNotEnumerate]
        [AllowUnsafeCall(typeof(JSObject))]
        public JSObject push(Arguments args)
        {
            notExists.valueType = JSObjectType.NotExistsInObject;
            if (this.GetType() == typeof(Array))
            {
                for (var i = 0; i < args.length; i++)
                {
                    if (data.Length == uint.MaxValue)
                    {
                        if (fields == null)
                            fields = createFields();
                        fields[uint.MaxValue.ToString()] = args.a0.CloneImpl();
                        throw new JSException(new RangeError("Invalid length of array"));
                    }
                    data.Add(args[i].CloneImpl());
                }
                return this.length;
            }
            else
            {
                var length = getLengthOfIterably(this, false);
                var i = length;
                length += args.length;
                this["length"] = length;
                for (var j = 0; i < length; i++, j++)
                    this[i.ToString()] = args[j].CloneImpl();
                return length;
            }
        }

        [DoNotEnumerate]
        [AllowUnsafeCall(typeof(JSObject))]
        public JSObject reverse()
        {
            Arguments args = null;
            if (this.GetType() == typeof(Array))
            {
                for (var i = data.Length >> 1; i-- > 0; )
                {
                    var item0 = data[(int)(data.Length - 1 - i)];
                    var item1 = data[(int)(i)];
                    JSObject value0, value1;
                    if (item0 == null || !item0.isExist)
                        item0 = __proto__[(data.Length - 1 - i).ToString()];
                    if (item0.valueType == JSObjectType.Property)
                        value0 = ((item0.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(this, null).CloneImpl();
                    else
                        value0 = item0;
                    if (item1 == null || !item1.isExist)
                        item1 = __proto__[i.ToString()];
                    if (item1.valueType == JSObjectType.Property)
                        value1 = ((item1.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(this, null).CloneImpl();
                    else
                        value1 = item1;
                    if (item0 != null && item0.valueType == JSObjectType.Property)
                    {
                        if (args == null)
                            args = new Arguments();
                        args.length = 1;
                        args.a0 = item1;
                        ((item0.oValue as PropertyPair).set ?? Function.emptyFunction).Invoke(this, args);
                    }
                    else if (value1.isExist)
                        data[(int)(data.Length - 1 - i)] = value1;
                    else if (item0 != null)
                        data[(int)(data.Length - 1 - i)] = null;

                    if (item1 != null && item1.valueType == JSObjectType.Property)
                    {
                        if (args == null)
                            args = new Arguments();
                        args.length = 1;
                        args.a0 = item0;
                        ((item1.oValue as PropertyPair).set ?? Function.emptyFunction).Invoke(this, args);
                    }
                    else if (value0.isExist)
                        data[(int)i] = value0;
                    else if (item1 != null)
                        data[(int)i] = null;
                }
                return this;
            }
            else
            {
                var length = getLengthOfIterably(this, false);
                if (length > 1)
                    for (var i = 0; i < length >> 1; i++)
                    {
                        JSObject i0 = i.ToString();
                        JSObject i1 = (length - 1 - i).ToString();
                        var item0 = this.GetMember(i0, false, false);
                        var item1 = this.GetMember(i1, false, false);
                        var value0 = item0;
                        var value1 = item1;
                        if (value0.valueType == JSObjectType.Property)
                            value0 = ((item0.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(this, null).CloneImpl();
                        else
                            value0 = value0.CloneImpl();
                        if (value1.valueType == JSObjectType.Property)
                            value1 = ((item1.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(this, null).CloneImpl();
                        else
                            value1 = value1.CloneImpl();

                        if (item0.valueType == JSObjectType.Property)
                        {
                            if (args == null)
                                args = new Arguments();
                            args.length = 1;
                            args.a0 = value1;
                            ((item0.oValue as PropertyPair).set ?? Function.emptyFunction).Invoke(this, args);
                        }
                        else if (value1.isExist)
                            this.GetMember(i0, true, true).Assign(value1);
                        else
                        {
                            var t = this.GetMember(i0, true, true);
                            if ((t.attributes & JSObjectAttributesInternal.DoNotDelete) == 0)
                            {
                                t.oValue = null;
                                t.valueType = JSObjectType.NotExists;
                            }
                        }
                        if (item1.valueType == JSObjectType.Property)
                        {
                            if (args == null)
                                args = new Arguments();
                            args.length = 1;
                            args.a0 = value0;
                            ((item1.oValue as PropertyPair).set ?? Function.emptyFunction).Invoke(this, args);
                        }
                        else if (value0.isExist)
                            this.GetMember(i1, true, true).Assign(value0);
                        else
                        {
                            var t = this.GetMember(i1, true, true);
                            if ((t.attributes & JSObjectAttributesInternal.DoNotDelete) == 0)
                            {
                                t.oValue = null;
                                t.valueType = JSObjectType.NotExists;
                            }
                        }
                    }
                return this;
            }
        }

        [DoNotEnumerate]
        [AllowUnsafeCall(typeof(JSObject))]
        public JSObject reduce(Arguments args)
        {
            JSObject _this = this;
            if (_this.valueType < JSObjectType.Object)
            {
                _this = new JSObject()
                {
                    valueType = JSObjectType.Object,
                    __proto__ = this.__proto__,
                    oValue = this
                };
            }
            var src = this;
            //if (this.GetType() != typeof(Array)) // ��� �������� ������ ���� �������� �������� �� ����������. 
            // ���� ������ ����������� ����������� �������, �� ����� ���� ������� ������� ������������������ � �������
            // ����� �������� ����� ������� � ��������� ������� ��� �� �����, �� � ���������
            src = iterableToArray(this, false, false, false);
            var func = args[0].oValue as Function;
            var accum = new JSObject() { valueType = JSObjectType.NotExists };
            if (args.length > 1)
                accum.Assign(args[1]);
            bool skip = false;
            if (accum.valueType < JSObjectType.Undefined)
            {
                if (src.data.Length == 0)
                    throw new JSException(new TypeError("Array is empty."));
                skip = true;
            }
            else if (src.data.Length == 0)
                return accum;
            if (func == null || func.valueType != JSObjectType.Function)
                throw new JSException(new TypeError("First argument on reduce mast be a function."));
            if (accum.GetType() != typeof(JSObject))
                accum = accum.CloneImpl();
            args.length = 4;
            args.a1 = new JSObject();
            args[2] = new JSObject();
            bool called = false;
            foreach (var element in (src.data as IEnumerable<KeyValuePair<int, JSObject>>))
            {
                if (this.GetType() == typeof(Array) && this.data.Length <= (uint)element.Key)
                    break;
                if (element.Value == null || !element.Value.isExist)
                    continue;
                args[0] = accum;
                if (element.Value.valueType == JSObjectType.Property)
                    args.a1.Assign((element.Value.oValue as PropertyPair).get == null ? undefined : (element.Value.oValue as PropertyPair).get.Invoke(this, null));
                else
                    args.a1.Assign(element.Value);
                called = true;
                if (skip)
                {
                    accum.Assign(args.a1);
                    skip = false;
                    continue;
                }
                if (element.Key >= 0)
                {
                    args.a2.valueType = JSObjectType.Int;
                    args.a2.iValue = element.Key;
                }
                else
                {
                    args.a2.valueType = JSObjectType.Double;
                    args.a2.dValue = (uint)element.Key;
                }
                args[3] = _this;
                accum.Assign(func.Invoke(args));
            }
            if (!called && skip)
                throw new JSException(new TypeError("Array is empty."));
            return accum;
        }

        [DoNotEnumerate]
        [AllowUnsafeCall(typeof(JSObject))]
        public JSObject reduceRight(Arguments args)
        {
            JSObject _this = this;
            if (_this.valueType < JSObjectType.Object)
            {
                _this = new JSObject()
                {
                    valueType = JSObjectType.Object,
                    __proto__ = this.__proto__,
                    oValue = this
                };
            }
            var src = this;
            //if (this.GetType() != typeof(Array))
            src = iterableToArray(this, false, false, false);
            var func = args[0].oValue as Function;
            var accum = new JSObject() { valueType = JSObjectType.NotExists };
            if (args.length > 1)
                accum.Assign(args[1]);
            var context = Context.CurrentContext;
            bool skip = false;
            if (accum.valueType < JSObjectType.Undefined)
            {
                if (src.data.Length == 0)
                    throw new JSException(new TypeError("Array is empty."));
                skip = true;
            }
            else if (src.data.Length == 0)
                return accum;
            if (func == null)
                throw new JSException(new TypeError("First argument on reduceRight mast be a function."));
            if (accum.GetType() != typeof(JSObject))
                accum = accum.CloneImpl();
            args.length = 4;
            args.a1 = new JSObject();
            args[2] = new JSObject();
            bool called = false;
            foreach (var element in src.data.Reversed)
            {
                if (this.GetType() == typeof(Array) && this.data.Length <= (uint)element.Key)
                    continue;
                if (element.Value == null || !element.Value.isExist)
                    continue;
                args[0] = accum;
                if (element.Value.valueType == JSObjectType.Property)
                    args.a1.Assign((element.Value.oValue as PropertyPair).get == null ? undefined : (element.Value.oValue as PropertyPair).get.Invoke(this, null));
                else
                    args.a1.Assign(element.Value);
                called = true;
                if (skip)
                {
                    accum.Assign(args.a1);
                    skip = false;
                    continue;
                }
                if (element.Key >= 0)
                {
                    args.a2.valueType = JSObjectType.Int;
                    args.a2.iValue = element.Key;
                }
                else
                {
                    args.a2.valueType = JSObjectType.Double;
                    args.a2.dValue = (uint)element.Key;
                }
                args[3] = _this;
                accum.Assign(func.Invoke(args));
            }
            if (!called && skip)
                throw new JSException(new TypeError("Array is empty."));
            return accum;
        }

        [DoNotEnumerate]
        [AllowUnsafeCall(typeof(JSObject))]
        public JSObject shift()
        {
            notExists.valueType = JSObjectType.NotExistsInObject;
            if (this.GetType() == typeof(Array))
            {
                if (data.Length == 0)
                    return notExists;
                var len = data.Length;
                var res = data[0];
                try
                {
                    if (res == null || !res.isExist)
                        res = __proto__["0"];
                }
                catch
                { }
                data[0] = null;
                if (res.valueType == JSObjectType.Property)
                    res = ((res.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(this, null);

                var source = this;
                var prew = 0U;
                foreach (var item in (data as IEnumerable<KeyValuePair<int, JSObject>>))
                {
                    if ((uint)item.Key >= len)
                        break;
                    if ((uint)item.Key - prew > 1)
                        break;
                    prew = (uint)item.Key;
                }
                if (len - prew > 1)
                    source = iterableToArray(this, false, false, false);
                JSObject prw = null;
                foreach (var item in (source.data as IEnumerable<KeyValuePair<int, JSObject>>))
                {
                    if (item.Key == 0)
                    {
                        prw = item.Value;
                        continue;
                    }
                    var value = item.Value;
                    if (value != null && value.valueType == JSObjectType.Property)
                        value = ((value.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(this, null);
                    if (prw != null && prw.valueType == JSObjectType.Property)
                    {
                        ((prw.oValue as PropertyPair).set ?? Function.emptyFunction).Invoke(this, new Arguments() { a0 = value, length = 1 });
                    }
                    else
                    {
                        if (item.Value != null)
                        {
                            if (item.Value.isExist)
                                data[item.Key - 1] = value;
                            if (item.Value.valueType != JSObjectType.Property)
                                data[item.Key] = null;
                        }
                    }
                    prw = item.Value;
                }
                if (len == 1)
                    data.Clear();
                else
                {
                    data.RemoveAt((int)len - 1);
                    data.Trim();
                    if (len - 1 > data.Length)
                        data[(int)len - 2] = data[(int)len - 2];
                }
                return res;
            }
            else
            {
                var lenObj = this["length"];
                if (lenObj.valueType == JSObjectType.Property)
                    lenObj = ((lenObj.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(this, null);
                long _length = (long)(uint)Tools.JSObjectToDouble(lenObj);
                if (_length > uint.MaxValue)
                    throw new JSException(new RangeError("Invalid array length"));
                if (_length == 0)
                {
                    this["length"] = lenObj = _length;
                    return notExists;
                }
                var ti = new JSObject() { valueType = JSObjectType.String, oValue = "0" };
                var t = this.GetMember(ti, true, false);
                var res = t;
                if (res.valueType == JSObjectType.Property)
                    res = ((res.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(this, null).CloneImpl();
                else
                    res = res.CloneImpl();
                if ((t.attributes & (JSObjectAttributesInternal.ReadOnly | JSObjectAttributesInternal.DoNotDelete)) == 0)
                {
                    t.oValue = null;
                    t.valueType = JSObjectType.NotExists;
                }
                if (_length == 1)
                {
                    this["length"] = lenObj = _length - 1;
                    return res;
                }
                var protoSource = iterableToArray(this, false, true, false);
                this["length"] = lenObj = _length - 1;

                List<string> keysToRemove = new List<string>();
                foreach (var key in this)
                {
                    var pindex = 0;
                    var dindex = 0.0;
                    long lindex = 0;
                    if (Tools.ParseNumber(key, ref pindex, out dindex)
                        && (pindex == key.Length)
                        && (lindex = (long)dindex) == dindex
                        && lindex < _length)
                    {
                        var temp = this[key];
                        if (!temp.isExist)
                            continue;
                        if (temp.valueType != JSObjectType.Property)
                            keysToRemove.Add(key);
                    }
                }
                var tjo = new JSObject() { valueType = JSObjectType.String };
                for (var i = 0; i < keysToRemove.Count; i++)
                {
                    tjo.oValue = keysToRemove[i];
                    var to = this.GetMember(tjo, true, false);
                    if ((to.attributes & (JSObjectAttributesInternal.ReadOnly | JSObjectAttributesInternal.DoNotDelete)) == 0)
                    {
                        to.oValue = null;
                        to.valueType = JSObjectType.NotExists;
                    }
                }
                tjo.valueType = JSObjectType.Int;
                foreach (var item in (protoSource.data as IEnumerable<KeyValuePair<int, JSObject>>))
                {
                    if ((uint)item.Key > int.MaxValue)
                    {
                        tjo.valueType = JSObjectType.Double;
                        tjo.dValue = (uint)(item.Key - 1);
                    }
                    else
                        tjo.iValue = (item.Key - 1);
                    if (item.Value != null && item.Value.isExist)
                    {
                        var temp = this.GetMember(tjo, true, false);
                        if (temp.valueType == JSObjectType.Property)
                            ((temp.oValue as PropertyPair).set ?? Function.emptyFunction).Invoke(this, new Arguments() { a0 = item.Value, length = 1 });
                        else
                            temp.Assign(item.Value);
                    }
                }
                return res;
            }
        }

        [DoNotEnumerate]
        [ParametersCount(2)]
        [AllowUnsafeCall(typeof(JSObject))]
        public JSObject slice(Arguments args)
        {
            if (args == null)
                throw new ArgumentNullException("args");
            JSObject src = this;
            if (!src.isDefinded || (src.valueType >= JSObjectType.Object && src.oValue == null))
                throw new JSException(new TypeError("Can not call Array.prototype.slice for null or undefined"));
            HashSet<string> processedKeys = null;
            Array res = new Array();
            for (; ; )
            {
                if (src.GetType() == typeof(Array))
                {
                    var startIndex = Tools.JSObjectToInt64(args[0], 0, true);
                    if (startIndex < 0)
                        startIndex += data.Length;
                    if (startIndex < 0)
                        startIndex = 0;
                    var endIndex = Tools.JSObjectToInt64(args[1], data.Length, true);
                    if (endIndex < 0)
                        endIndex += data.Length;
                    var len = data.Length;
                    foreach (var element in ((src as Array).data as IEnumerable<KeyValuePair<int, JSObject>>))
                    {
                        if (element.Key >= len) // ���...
                            break;
                        var value = element.Value;
                        if (value == null || !value.isExist)
                            continue;
                        if (value.valueType == JSObjectType.Property)
                            value = (value.oValue as PropertyPair).get == null ? undefined : (value.oValue as PropertyPair).get.Invoke(src, null);
                        if (processedKeys != null)
                        {
                            var sk = element.Key.ToString();
                            if (processedKeys.Contains(sk))
                                continue;
                            processedKeys.Add(sk);
                        }
                        if (element.Key >= startIndex && element.Key < endIndex)
                            res.data[element.Key - (int)startIndex] = element.Value.CloneImpl();
                    }
                }
                else
                {
                    var lenObj = this["length"]; // ��� �� �������� �� null/undefined � �������� ���� ����
                    if (!lenObj.isDefinded)
                        return new Array();
                    if (lenObj.valueType == JSObjectType.Property)
                        lenObj = ((lenObj.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(this, null);
                    if (lenObj.valueType >= JSObjectType.Object)
                        lenObj = lenObj.ToPrimitiveValue_Value_String();
                    if (!lenObj.isDefinded)
                        return new Array();
                    long _length = (uint)Tools.JSObjectToInt64(lenObj);
                    var startIndex = Tools.JSObjectToInt64(args[0], 0, true);
                    if (startIndex < 0)
                        startIndex += _length;
                    if (startIndex < 0)
                        startIndex = 0;
                    var endIndex = Tools.JSObjectToInt64(args[1], _length, true);
                    if (endIndex < 0)
                        endIndex += _length;
                    var @enum = src.GetEnumeratorImpl(false);
                    while (@enum.MoveNext())
                    {
                        var i = @enum.Current;
                        var pindex = 0;
                        var dindex = 0.0;
                        long lindex = 0;
                        if (Tools.ParseNumber(i, ref pindex, out dindex)
                            && (pindex == i.Length)
                            && dindex < _length
                            && (lindex = (long)dindex) == dindex)
                        {
                            var temp = src[i];
                            if (temp.valueType == JSObjectType.Property)
                                temp = ((temp.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(src, null);
                            if (!temp.isExist)
                                continue;
                            if (processedKeys != null)
                            {
                                var sk = lindex.ToString();
                                if (processedKeys.Contains(sk))
                                    continue;
                                processedKeys.Add(sk);
                            }
                            if (lindex >= startIndex && lindex < endIndex)
                                res.data[(int)lindex - (int)startIndex] = temp.CloneImpl();
                        }
                    }
                }
                var crnt = src;
                if (src.__proto__ == Null)
                    break;
                src = src.__proto__;
                if (src == null || (src.valueType >= JSObjectType.String && src.oValue == null))
                    break;
                if (processedKeys == null)
                    processedKeys = new HashSet<string>(crnt);
            }
            return res;
        }

        [DoNotEnumerate]
        [ParametersCount(2)]
        [AllowUnsafeCall(typeof(JSObject))]
        public JSObject splice(Arguments args)
        {
            if (args == null)
                throw new ArgumentNullException("args");
            if (args.Length == 0)
                return this;
            if (this.GetType() == typeof(Array)) // ��, Array sealed, �� ��� � �� ����� ��������.
            {
                long pos0 = (long)System.Math.Min(Tools.JSObjectToDouble(args[0]), data.Length);
                long pos1 = 0;
                if (args.Length > 1)
                {
                    if (args[1].valueType <= JSObjectType.Undefined)
                        pos1 = 0;
                    else
                        pos1 = (long)System.Math.Min(Tools.JSObjectToDouble(args[1]), data.Length);
                }
                else
                    pos1 = data.Length;
                if (pos0 < 0)
                    pos0 = data.Length + pos0;
                if (pos0 < 0)
                    pos0 = 0;
                if (pos1 < 0)
                    pos1 = 0;
                if (pos1 == 0 && args.length <= 2)
                    return new Array();
                pos0 = (uint)System.Math.Min(pos0, data.Length);
                pos1 += pos0;
                pos1 = (uint)System.Math.Min(pos1, data.Length);
                var res = new Array((int)(pos1 - pos0));
                var delta = args.length - (pos1 - pos0) - 2;
                List<KeyValuePair<int, JSObject>> relocated = null;
                if (delta > 0)
                    relocated = new List<KeyValuePair<int, JSObject>>();
                foreach (var node in (data as IEnumerable<KeyValuePair<int, JSObject>>))
                {
                    if (node.Value != null && node.Value.isExist && node.Key >= pos0)
                    {
                        if (node.Key >= pos1 && delta == 0)
                            break;
                        var value = node.Value;
                        if (value != null && value.valueType == JSObjectType.Property)
                            value = ((value.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(this, null).CloneImpl();
                        if (node.Key < pos1)
                            res.data[(int)(node.Key - pos0)] = value;
                        else
                        {
                            if (delta > 0)
                                relocated.Add(new KeyValuePair<int, JSObject>(node.Key, value));
                            else
                            {
                                var t = data[(int)(node.Key + delta)];
                                if (t != null && t.valueType == JSObjectType.Property)
                                    ((t.oValue as PropertyPair).set ?? Function.emptyFunction).Invoke(this, new Arguments() { a0 = value, length = 1 });
                                else
                                    data[(int)(node.Key + delta)] = value;
                            }
                        }
                    }
                }
                if (delta > 0)
                {
                    for (var i = 0; i < relocated.Count; i++)
                    {
                        var t = data[(int)(relocated[i].Key + delta)];
                        if (t != null && t.valueType == JSObjectType.Property)
                            ((t.oValue as PropertyPair).set ?? Function.emptyFunction).Invoke(this, new Arguments() { a0 = relocated[i].Value, length = 1 });
                        else
                            data[(int)(relocated[i].Key + delta)] = relocated[i].Value;
                    }
                }
                else while (delta++ < 0)
                        data.RemoveAt((int)(data.Length - 1));
                for (var i = 2; i < args.length; i++)
                    if (args[i].isExist)
                    {
                        var t = data[(int)(pos0 + i - 2)];
                        if (t != null && t.valueType == JSObjectType.Property)
                            ((t.oValue as PropertyPair).set ?? Function.emptyFunction).Invoke(this, new Arguments() { a0 = args[i], length = 1 });
                        else
                            data[(int)(pos0 + i - 2)] = args[i].CloneImpl();
                    }
                return res;
            }
            else // ���-�� �������� ������ � ����� length
            {
                long _length = getLengthOfIterably(this, false);
                var pos0 = (long)System.Math.Min(Tools.JSObjectToDouble(args[0]), _length);
                long pos1 = 0;
                if (args.Length > 1)
                {
                    if (args[1].valueType <= JSObjectType.Undefined)
                        pos1 = 0;
                    else
                        pos1 = (long)System.Math.Min(Tools.JSObjectToDouble(args[1]), _length);
                }
                else
                    pos1 = _length;
                if (pos0 < 0)
                    pos0 = _length + pos0;
                if (pos0 < 0)
                    pos0 = 0;
                if (pos1 < 0)
                    pos1 = 0;
                if (pos1 == 0 && args.length <= 2)
                {
                    var lenobj = this.GetMember("length", true, false);
                    if (lenobj.valueType == JSObjectType.Property)
                        ((lenobj.oValue as PropertyPair).set ?? Function.emptyFunction).Invoke(this, new Arguments() { a0 = _length, length = 1 });
                    else
                        lenobj.Assign(_length);
                    return new Array();
                }
                pos0 = (uint)System.Math.Min(pos0, _length);
                pos1 += pos0;
                pos1 = (uint)System.Math.Min(pos1, _length);
                var delta = System.Math.Max(0, args.length - 2) - (pos1 - pos0);
                var res = new Array();
                long prewKey = -1;
                foreach (var keyS in iterablyEnum(_length, this))
                {
                    if (prewKey == -1)
                        prewKey = (uint)keyS.Key;
                    if (keyS.Key - prewKey > 1 && keyS.Key < pos1)
                    {
                        for (var i = prewKey + 1; i < keyS.Key; i++)
                        {
                            var value = __proto__[i.ToString()];
                            if (value.valueType == JSObjectType.Property)
                                value = ((value.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(__proto__, null).CloneImpl();
                            else
                                value = value.CloneImpl();
                            res.data[(int)i] = value.CloneImpl();
                        }
                    }
                    if (keyS.Key >= pos1)
                        break;
                    else if (pos0 <= keyS.Key)
                    {
                        var value = this[keyS.Value];
                        if (value.ValueType == JSObjectType.Property)
                            value = ((value.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(this, null).CloneImpl();
                        else
                            value = value.CloneImpl();
                        res.data[(int)(keyS.Key - pos0)] = value;
                    }
                    prewKey = keyS.Key;
                }
                if (prewKey == -1)
                {
                    for (var i = 0; i < (pos1 - pos0); i++)
                        res.Add(__proto__[(i + pos0).ToString()].CloneImpl());
                }
                var tjo = new JSObject();
                if (delta > 0)
                {
                    for (var i = _length; i-- > pos1; )
                    {
                        if (i <= int.MaxValue)
                        {
                            tjo.valueType = JSObjectType.Int;
                            tjo.iValue = (int)(i + delta);
                        }
                        else
                        {
                            tjo.valueType = JSObjectType.Double;
                            tjo.dValue = i + delta;
                        }
                        var dst = this.GetMember(tjo, true, false);
                        if (i + delta <= int.MaxValue)
                        {
                            tjo.valueType = JSObjectType.Int;
                            tjo.iValue = (int)(i);
                        }
                        else
                        {
                            tjo.valueType = JSObjectType.Double;
                            tjo.dValue = i;
                        }
                        var src = this.GetMember(tjo, true, false);
                        if (src.valueType == JSObjectType.Property)
                            src = ((src.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(this, null);
                        if (dst.valueType == JSObjectType.Property)
                            ((dst.oValue as PropertyPair).set ?? Function.emptyFunction).Invoke(this, new Arguments() { a0 = src, length = 1 });
                        else
                            dst.Assign(src);
                    }
                }
                else if (delta < 0)
                {
                    for (var i = pos0; i < pos1; i++)
                    {
                        if (i + delta <= int.MaxValue)
                        {
                            tjo.valueType = JSObjectType.Int;
                            tjo.iValue = (int)(i);
                        }
                        else
                        {
                            tjo.valueType = JSObjectType.Double;
                            tjo.dValue = i;
                        }
                        var src = this.GetMember(tjo, true, false);
                        if (i >= _length + delta)
                        {
                            if ((src.attributes & JSObjectAttributesInternal.DoNotDelete) == 0)
                            {
                                src.valueType = JSObjectType.NotExists;
                                src.oValue = null;
                            }
                        }
                    }

                    for (var i = pos1; i < _length; i++)
                    {
                        if (i <= int.MaxValue)
                        {
                            tjo.valueType = JSObjectType.Int;
                            tjo.iValue = (int)(i + delta);
                        }
                        else
                        {
                            tjo.valueType = JSObjectType.Double;
                            tjo.dValue = i + delta;
                        }
                        var dst = this.GetMember(tjo, true, false);
                        if (i + delta <= int.MaxValue)
                        {
                            tjo.valueType = JSObjectType.Int;
                            tjo.iValue = (int)(i);
                        }
                        else
                        {
                            tjo.valueType = JSObjectType.Double;
                            tjo.dValue = i;
                        }
                        var srcItem = this.GetMember(tjo, true, false);
                        var src = srcItem;
                        if (src.valueType == JSObjectType.Property)
                            src = ((src.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(this, null);
                        if (dst.valueType == JSObjectType.Property)
                            ((dst.oValue as PropertyPair).set ?? Function.emptyFunction).Invoke(this, new Arguments() { a0 = src, length = 1 });
                        else
                            dst.Assign(src);
                        if (i >= _length + delta)
                        {
                            if ((srcItem.attributes & JSObjectAttributesInternal.DoNotDelete) == 0)
                            {
                                srcItem.valueType = JSObjectType.NotExists;
                                srcItem.oValue = null;
                            }
                        }
                    }
                }
                for (var i = 2; i < args.length; i++)
                {
                    if ((i - 2 + pos0) <= int.MaxValue)
                    {
                        tjo.valueType = JSObjectType.Int;
                        tjo.iValue = (int)(i - 2 + pos0);
                    }
                    else
                    {
                        tjo.valueType = JSObjectType.Double;
                        tjo.dValue = (i - 2 + pos0);
                    }
                    var dst = this.GetMember(tjo, true, false);
                    if (dst.valueType == JSObjectType.Property)
                        ((dst.oValue as PropertyPair).set ?? Function.emptyFunction).Invoke(this, new Arguments() { a0 = args[i], length = 1 });
                    else
                        dst.Assign(args[i]);
                }
                {
                    _length += delta;
                    var lenobj = this.GetMember("length", true, false);
                    if (lenobj.valueType == JSObjectType.Property)
                        ((lenobj.oValue as PropertyPair).set ?? Function.emptyFunction).Invoke(this, new Arguments() { a0 = _length, length = 1 });
                    else
                        lenobj.Assign(_length);
                }
                return res;
            }
        }

        private sealed class JSComparer : IComparer<JSObject>
        {
            Arguments args;
            JSObject first;
            JSObject second;
            Function comparer;

            public JSComparer(Arguments args, JSObject first, JSObject second, Function comparer)
            {
                this.args = args;
                this.first = first;
                this.second = second;
                this.comparer = comparer;
            }

            public int Compare(JSObject x, JSObject y)
            {
                first.Assign(x);
                second.Assign(y);
                args[0] = first;
                args[1] = second;
                var res = Tools.JSObjectToInt32(comparer.Invoke(JSObject.undefined, args));
                return res;
            }
        }

        [DoNotEnumerate]
        [AllowUnsafeCall(typeof(JSObject))]
        public JSObject sort(Arguments args)
        {
            if (args == null)
                throw new ArgumentNullException("args");
            var comparer = args[0].oValue as Function;
            if (this.GetType() == typeof(Array))
            {
                var len = data.Length;
                if (comparer != null)
                {
                    var second = new JSObject();
                    var first = new JSObject();
                    args.length = 2;
                    args[0] = first;
                    args[1] = second;

                    var tt = new BinaryTree<JSObject, List<JSObject>>(new JSComparer(args, first, second, comparer));
                    uint length = data.Length;
                    var source = this;
                    foreach (var item in (source.data as IEnumerable<KeyValuePair<int, JSObject>>))
                    {
                        if (item.Value == null || !item.Value.isDefinded)
                            continue;
                        var v = item.Value;
                        if (v.valueType == JSObjectType.Property)
                            v = ((v.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(this, null).CloneImpl();
                        List<JSObject> list = null;
                        if (!tt.TryGetValue(v, out list))
                            tt[v] = list = new List<JSObject>();
                        list.Add(item.Value);
                    }
                    data.Clear();
                    foreach (var node in tt.Nodes)
                    {
                        for (var i = node.value.Count; i-- > 0; )
                            data.Add(node.value[i]);
                    }
                    data[(int)length - 1] = data[(int)length - 1];
                }
                else
                {
                    var tt = new BinaryTree<string, List<JSObject>>(StringComparer.Ordinal);
                    uint length = data.Length;
                    var source = this;
                    foreach (var item in (source.data as IEnumerable<KeyValuePair<int, JSObject>>))
                    {
                        if (item.Value == null || !item.Value.isExist)
                            continue;
                        var v = item.Value;
                        if (v.valueType == JSObjectType.Property)
                            v = ((v.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(this, null).CloneImpl();
                        List<JSObject> list = null;
                        var key = v.ToString();
                        if (!tt.TryGetValue(key, out list))
                            tt[key] = list = new List<JSObject>();
                        list.Add(item.Value);
                    }
                    data.Clear();
                    foreach (var node in tt.Nodes)
                    {
                        for (var i = node.value.Count; i-- > 0; )
                            data.Add(node.value[i]);
                    }
                    data[(int)length - 1] = data[(int)length - 1];
                }
            }
            else
            {
                var len = getLengthOfIterably(this, false);
                if (comparer != null)
                {
                    var second = new JSObject();
                    var first = new JSObject();
                    args.length = 2;
                    args[0] = first;
                    args[1] = second;

                    var tt = new BinaryTree<JSObject, List<JSObject>>(new JSComparer(args, first, second, comparer));
                    List<string> keysToRemove = new List<string>();
                    foreach (var key in iterablyEnum(len, this))
                    {
                        keysToRemove.Add(key.Value);
                        var item = this[key.Value];
                        if (item.isDefinded)
                        {
                            item = item.CloneImpl();
                            JSObject value;
                            if (item.valueType == JSObjectType.Property)
                                value = ((item.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(this, null);
                            else
                                value = item;
                            List<JSObject> els = null;
                            if (!tt.TryGetValue(value, out els))
                                tt[value] = els = new List<JSObject>();
                            els.Add(item);
                        }
                    }
                    var tjo = new JSObject() { valueType = JSObjectType.String };
                    for (var i = keysToRemove.Count; i-- > 0; )
                    {
                        tjo.oValue = keysToRemove[i];
                        var t = this.GetMember(tjo, true, false);
                        if ((t.attributes & JSObjectAttributesInternal.DoNotDelete) == 0)
                        {
                            t.oValue = null;
                            t.valueType = JSObjectType.NotExists;
                        }
                    }
                    var index = 0u;
                    foreach (var node in tt.Nodes)
                    {
                        for (var i = node.value.Count; i-- > 0; )
                            this[(index++).ToString()] = node.value[i];
                    }
                }
                else
                {
                    var tt = new BinaryTree<string, List<JSObject>>(StringComparer.Ordinal);
                    List<string> keysToRemove = new List<string>();
                    foreach (var key in this)
                    {
                        var pindex = 0;
                        var dindex = 0.0;
                        if (Tools.ParseNumber(key, ref pindex, out dindex) && (pindex == key.Length)
                            && dindex < len)
                        {
                            keysToRemove.Add(key);
                            var value = this[key];
                            if (value.isDefinded)
                            {
                                value = value.CloneImpl();
                                List<JSObject> els = null;
                                var skey = value.ToString();
                                if (!tt.TryGetValue(skey, out els))
                                    tt[skey] = els = new List<JSObject>();
                                els.Add(value);
                            }
                        }
                    }
                    for (var i = keysToRemove.Count; i-- > 0; )
                        this[keysToRemove[i]].valueType = JSObjectType.NotExists;
                    var index = 0u;
                    foreach (var node in tt.Nodes)
                    {
                        for (var i = node.value.Count; i-- > 0; )
                            this[(index++).ToString()] = node.value[i];
                    }
                }
            }
            return this;
        }

        [DoNotEnumerate]
        [AllowUnsafeCall(typeof(JSObject))]
        public JSObject unshift(Arguments args)
        {
            if (args == null)
                throw new ArgumentNullException();
            if (this.GetType() == typeof(Array))
            {
                if (args.length == 0)
                    return data.Length;
                if (data.Length + args.length > uint.MaxValue)
                    throw new JSException(new RangeError("Invalid array length"));
                var len = data.Length;
                var prewIndex = -1;
                foreach (var node in data.Reversed)
                {
                    if (prewIndex == -1)
                        prewIndex = node.Key;
                    else if (prewIndex - node.Key > 1)
                    {
                        for (var i = node.Key + 1; i < prewIndex; i++)
                        {
                            var t = __proto__[i.ToString()];
                            if (t.isExist)
                                data[(int)(i + args.Length)] = t.CloneImpl();
                        }
                    }
                    data[(int)node.Key] = null;
                    if (node.Value != null && node.Value.isExist)
                        data[(int)(node.Key + args.Length)] = node.Value;
                    else
                    {
                        var t = __proto__[node.Key.ToString()];
                        if (t.isExist)
                            data[(int)(node.Key + args.Length)] = t.CloneImpl();
                    }
                    prewIndex = node.Key;
                }
                data[(int)(args.Length + len - 1)] = data[(int)(args.Length + len - 1)];
                for (var i = 0; i < args.Length; i++)
                    data[i] = args[i].CloneImpl();
                return length;
            }
            else
            {
                var lenObj = this["length"];
                if (lenObj.valueType == JSObjectType.Property)
                    lenObj = ((lenObj.oValue as PropertyPair).get ?? Function.emptyFunction).Invoke(this, null);
                long _length = (long)(uint)Tools.JSObjectToDouble(this["length"]);
                if (_length + args.length > uint.MaxValue)
                    throw new JSException(new RangeError("Invalid array length"));
                this["length"] = lenObj = _length;
                if (args.length == 0)
                    return lenObj;
                List<KeyValuePair<KeyValuePair<long, string>, JSObject>> keysToRemove = new List<KeyValuePair<KeyValuePair<long, string>, JSObject>>();
                long prewKey = -1;
                foreach (var key in this)
                {
                    var pindex = 0;
                    var dindex = 0.0;
                    if (Tools.ParseNumber(key, ref pindex, out dindex) && (pindex == key.Length)
                        && dindex < _length
                        && (long)dindex == dindex)
                    {
                        if (prewKey == -1)
                            prewKey = (uint)dindex;
                        if (dindex - prewKey > 1)
                        {
                            for (var i = prewKey + 1; i < dindex; i++)
                            {
                                var t = __proto__[i.ToString()];
                                if (t.isExist)
                                    this[(i + args.Length).ToString()] = t.CloneImpl();
                            }
                        }
                        keysToRemove.Add(new KeyValuePair<KeyValuePair<long, string>, JSObject>(new KeyValuePair<long, string>((long)dindex, key), this[key].CloneImpl()));
                    }
                }
                if (prewKey == -1)
                {
                    for (var i = 0; i < _length; i++)
                    {
                        var t = __proto__[i.ToString()];
                        if (t.isExist)
                            this[(i + args.Length).ToString()] = t.CloneImpl();
                    }
                }
                for (var i = keysToRemove.Count; i-- > 0; )
                    this[keysToRemove[i].Key.Value] = notExists;
                for (var i = keysToRemove.Count; i-- > 0; )
                {
                    this[(keysToRemove[i].Key.Key + args.length).ToString(CultureInfo.InvariantCulture)] = keysToRemove[i].Value;
                }
                for (var i = 0; i < args.Length; i++)
                    this[i.ToString()] = args[i].CloneImpl();
                _length += args.length;
                this["length"] = _length;
                return _length;
            }
        }

        [Hidden]
        public override string ToString()
        {
            return joinImpl(",", false);
        }

        [DoNotEnumerate]
        [CLSCompliant(false)]
        [ParametersCount(0)]
        public override JSObject toString(Arguments args)
        {
            if (this.GetType() != typeof(Array) && !this.GetType().IsSubclassOf(typeof(Array)))
                throw new JSException(new TypeError("Try to call Array.toString on not Array object."));
            return this.ToString();
        }

        [DoNotEnumerate]
        public override JSObject toLocaleString()
        {
            return joinImpl(",", true);
        }

        protected internal override IEnumerator<string> GetEnumeratorImpl(bool hideNonEnum)
        {
            foreach (var node in (data as IEnumerable<KeyValuePair<int, JSObject>>))
            {
                if (node.Value != null
                    && node.Value.isExist
                    && (!hideNonEnum || (node.Value.attributes & JSObjectAttributesInternal.DoNotEnum) == 0))
                    yield return ((uint)node.Key).ToString();
            }
            if (!hideNonEnum)
                yield return "length";
            if (fields != null)
            {
                foreach (var f in fields)
                {
                    if (f.Value.isExist && (!hideNonEnum || (f.Value.attributes & JSObjectAttributesInternal.DoNotEnum) == 0))
                        yield return f.Key;
                }
            }
        }

        [Hidden]
        internal protected override JSObject GetMember(JSObject name, bool forWrite, bool own)
        {
            if (name.valueType == JSObjectType.String && "length".Equals(name.oValue))
                return length;
            bool isIndex = false;
            int index = 0;
            JSObject tname = name;
            if (tname.valueType >= JSObjectType.Object)
                tname = tname.ToPrimitiveValue_String_Value();
            switch (tname.valueType)
            {
                case JSObjectType.Object:
                case JSObjectType.Bool:
                    break;
                case JSObjectType.Int:
                    {
                        isIndex = tname.iValue >= 0;
                        index = tname.iValue;
                        break;
                    }
                case JSObjectType.Double:
                    {
                        isIndex = tname.dValue >= 0 && tname.dValue < uint.MaxValue && (long)tname.dValue == tname.dValue;
                        if (isIndex)
                            index = (int)(uint)tname.dValue;
                        break;
                    }
                case JSObjectType.String:
                    {
                        var fc = tname.oValue.ToString()[0];
                        if ('0' <= fc && '9' >= fc)
                        {
                            var dindex = 0.0;
                            int si = 0;
                            if (Tools.ParseNumber(tname.oValue.ToString(), ref si, out dindex)
                                && (si == tname.oValue.ToString().Length)
                                && dindex >= 0
                                && dindex < uint.MaxValue
                                && (long)dindex == dindex)
                            {
                                isIndex = true;
                                index = (int)(uint)dindex;
                            }
                        }
                        break;
                    }
            }
            if (isIndex)
            {
                forWrite &= (attributes & JSObjectAttributesInternal.Immutable) == 0;
                if (forWrite)
                {
                    if (_lengthObj != null && (_lengthObj.attributes & JSObjectAttributesInternal.ReadOnly) != 0 && index >= data.Length)
                    {
                        if (own)
                            throw new JSException(new TypeError("Cannot add element in fixed size array"));
                        return notExists;
                    }
                    var res = data[(int)index];
                    if (res == null)
                    {
                        res = new JSObject() { valueType = JSObjectType.NotExistsInObject };
                        data[(int)index] = res;
                    }
                    else if ((res.attributes & JSObjectAttributesInternal.SystemObject) != 0)
                        data[(int)index] = res = res.CloneImpl();
                    return res;
                }
                else
                {
                    notExists.valueType = JSObjectType.NotExistsInObject;
                    var res = data[(int)index] ?? notExists;
                    if (!res.isExist && !own)
                        return __proto__.GetMember(name, forWrite, own);
                    return res;
                }
            }

            //if ((attributes & JSObjectAttributesInternal.ProxyPrototype) != 0)
            //    return __proto__.GetMember(name, create, own);
            return DefaultFieldGetter(name, forWrite, own);
        }

        [Hidden]
        public override JSObject valueOf()
        {
            return base.valueOf();
        }
    }
}