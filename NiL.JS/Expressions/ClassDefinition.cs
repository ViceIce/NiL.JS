﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using NiL.JS.BaseLibrary;
using NiL.JS.Core;
using NiL.JS.Core.Interop;
using NiL.JS.Extensions;

namespace NiL.JS.Expressions
{
#if !PORTABLE
    [Serializable]
#endif
    public sealed class MemberDescriptor
    {
        internal Expression _name;
        internal Expression _value;
        internal bool _static;

        public Expression Name { get { return _name; } }
        public Expression Value { get { return _value; } }
        public bool Static { get { return _static; } }

        public MemberDescriptor(Expression name, Expression value, bool @static)
        {
            _name = name;
            _value = value;
            _static = @static;
        }

        public override string ToString()
        {
            if (_static)
                return "static " + _value;
            return _value.ToString();
        }
    }

#if !PORTABLE
    [Serializable]
#endif
    public sealed class ClassDefinition : EntityDefinition
    {
        private sealed class ClassConstructor : Function
        {
            public ClassConstructor(Context context, FunctionDefinition creator)
                : base(context, creator)
            {

            }

            protected internal override JSValue ConstructObject()
            {
                return new ObjectWrapper(null)
                {
                    __proto__ = prototype.oValue as JSObject,
                    ownedFieldsOnly = true
                };
            }
        }

        protected internal override PredictedType ResultType
        {
            get
            {
                return PredictedType.Function;
            }
        }

        internal override bool ResultInTempContainer
        {
            get { return false; }
        }

        protected internal override bool ContextIndependent
        {
            get
            {
                return false;
            }
        }

        protected internal override bool NeedDecompose
        {
            get
            {
                if (_constructor.NeedDecompose)
                    return true;

                for (var i = 0; i < members.Length; i++)
                {
                    if (members[i]._value.NeedDecompose)
                        return true;
                }

                return false;
            }
        }

        private MemberDescriptor[] members;
        private Expression _baseClass;
        private FunctionDefinition _constructor;
        private MemberDescriptor[] computedProperties;

        public IEnumerable<MemberDescriptor> Members { get { return members; } }
        public Expression BaseClass { get { return _baseClass; } }
        public FunctionDefinition Constructor { get { return _constructor; } }
        public IEnumerable<MemberDescriptor> ComputedProperties { get { return computedProperties; } }

        private ClassDefinition(string name, Expression baseType, MemberDescriptor[] fields, FunctionDefinition ctor, MemberDescriptor[] computedProperties)
        {
            this.name = name;
            this._baseClass = baseType;
            this._constructor = ctor;
            this.members = fields;
            this.computedProperties = computedProperties;
        }

        internal static CodeNode Parse(ParseInfo state, ref int index)
        {
            string code = state.Code;
            int i = index;
            if (!Parser.Validate(code, "class", ref i))
                return null;
            while (Tools.IsWhiteSpace(code[i]))
                i++;
            string name = null;
            Expression baseType = null;
            if (!Parser.Validate(code, "extends ", i))
            {
                var n = i;
                if (Parser.ValidateName(code, ref i, true))
                    name = code.Substring(n, i - n);

                while (Tools.IsWhiteSpace(code[i]))
                    i++;
            }
            if (Parser.Validate(code, "extends ", ref i))
            {
                var n = i;
                if (!Parser.ValidateName(code, ref i, true) && !Parser.Validate(code, "null", ref i))
                    ExceptionsHelper.ThrowSyntaxError("Invalid base class name", state.Code, i);
                var baseClassName = code.Substring(n, i - n);
                if (baseClassName == "null")
                    baseType = new ConstantDefinition(JSValue.@null) { Position = n, Length = 4 };
                else
                    baseType = new GetVariableExpression(baseClassName, state.lexicalScopeLevel);
                while (Tools.IsWhiteSpace(code[i]))
                    i++;
            }
            if (code[i] != '{')
                ExceptionsHelper.ThrowSyntaxError(Strings.UnexpectedToken, code, i);

            CodeNode ctor = null;
            var oldStrict = state.strict;
            state.strict = true;
            var flds = new Dictionary<string, MemberDescriptor>();
            var computedProperties = new List<MemberDescriptor>();
            var oldCodeContext = state.CodeContext;

            while (code[i] != '}')
            {
                do
                    i++;
                while (Tools.IsWhiteSpace(code[i]) || code[i] == ';');
                int s = i;
                if (state.Code[i] == '}')
                    break;

                bool @static = Parser.Validate(state.Code, "static", ref i);
                if (@static)
                {
                    while (Tools.IsWhiteSpace(state.Code[i]))
                        i++;
                }
                bool getOrSet = Parser.Validate(state.Code, "get", ref i) || Parser.Validate(state.Code, "set", ref i);
                if (getOrSet)
                {
                    while (Tools.IsWhiteSpace(state.Code[i]))
                        i++;
                }
                var asterisk = state.Code[i] == '*';
                if (asterisk)
                {
                    do
                        i++;
                    while (Tools.IsWhiteSpace(state.Code[i]));
                }

                if (Parser.Validate(state.Code, "[", ref i))
                {
                    var propertyName = ExpressionTree.Parse(state, ref i, false, false, false, true, false, false);
                    while (Tools.IsWhiteSpace(state.Code[i]))
                        i++;
                    if (state.Code[i] != ']')
                        ExceptionsHelper.ThrowSyntaxError("Expected ']'", state.Code, i);
                    do
                        i++;
                    while (Tools.IsWhiteSpace(state.Code[i]));

                    CodeNode initializer;
                    if (state.Code[i] == '(')
                    {
                        initializer = FunctionDefinition.Parse(state, ref i, asterisk ? FunctionKind.AnonymousGenerator : FunctionKind.AnonymousFunction);
                    }
                    else
                    {
                        initializer = ExpressionTree.Parse(state, ref i);
                    }

                    switch (state.Code[s])
                    {
                        case 'g':
                            {
                                computedProperties.Add(new MemberDescriptor((Expression)propertyName, new GsPropertyPairExpression((Expression)initializer, null), @static));
                                break;
                            }
                        case 's':
                            {
                                computedProperties.Add(new MemberDescriptor((Expression)propertyName, new GsPropertyPairExpression(null, (Expression)initializer), @static));
                                break;
                            }
                        default:
                            {
                                computedProperties.Add(new MemberDescriptor((Expression)propertyName, (Expression)initializer, @static));
                                break;
                            }
                    }
                }
                else if (getOrSet)
                {
                    i = s;
                    var mode = state.Code[i] == 's' ? FunctionKind.Setter : FunctionKind.Getter;
                    var propertyAccessor = FunctionDefinition.Parse(state, ref i, mode) as FunctionDefinition;
                    var accessorName = (@static ? "static " : "") + propertyAccessor.name;
                    if (!flds.ContainsKey(accessorName))
                    {
                        var propertyPair = new GsPropertyPairExpression
                        (
                            mode == FunctionKind.Getter ? propertyAccessor : null,
                            mode == FunctionKind.Setter ? propertyAccessor : null
                        );
                        flds.Add(accessorName, new MemberDescriptor(new ConstantDefinition(propertyAccessor.name), propertyPair, @static));
                    }
                    else
                    {
                        var vle = flds[accessorName].Value as GsPropertyPairExpression;

                        if (vle == null)
                            ExceptionsHelper.Throw((new SyntaxError("Try to define " + mode.ToString().ToLowerInvariant() + " for defined field at " + CodeCoordinates.FromTextPosition(state.Code, s, 0))));

                        do
                        {
                            if (mode == FunctionKind.Getter)
                            {
                                if (vle.Getter == null)
                                {
                                    vle.Getter = propertyAccessor;
                                    break;
                                }
                            }
                            else
                            {
                                if (vle.Setter == null)
                                {
                                    vle.Setter = propertyAccessor;
                                    break;
                                }
                            }

                            ExceptionsHelper.ThrowSyntaxError("Try to redefine " + mode.ToString().ToLowerInvariant() + " of " + propertyAccessor.Name, state.Code, s);
                        }
                        while (false);
                    }
                }
                else
                {
                    i = s;
                    string fieldName = null;
                    if (state.Code[i] == '*')
                    {
                        do
                            i++;
                        while (Tools.IsWhiteSpace(code[i]));
                    }

                    if (Parser.ValidateName(state.Code, ref i, false, true, state.strict))
                        fieldName = Tools.Unescape(state.Code.Substring(s, i - s), state.strict);
                    else if (Parser.ValidateValue(state.Code, ref i))
                    {
                        double d = 0.0;
                        int n = s;
                        if (Tools.ParseNumber(state.Code, ref n, out d))
                            fieldName = Tools.DoubleToString(d);
                        else if (state.Code[s] == '\'' || state.Code[s] == '"')
                            fieldName = Tools.Unescape(state.Code.Substring(s + 1, i - s - 2), state.strict);
                    }

                    if (fieldName == null)
                        ExceptionsHelper.Throw((new SyntaxError("Invalid member name at " + CodeCoordinates.FromTextPosition(state.Code, s, i - s))));

                    if (fieldName == "constructor")
                    {
                        if (@static)
                        {
                            ExceptionsHelper.ThrowSyntaxError(Strings.ConstructorCannotBeStatic, state.Code, s);
                        }
                        if (ctor != null)
                        {
                            ExceptionsHelper.ThrowSyntaxError("Trying to redefinition constructor", state.Code, s);
                        }

                        state.CodeContext |= CodeContext.InClassConstructor;
                    }
                    else if (@static)
                    {
                        fieldName = "static " + fieldName;
                        state.CodeContext |= CodeContext.InStaticMember;
                    }
                    if (flds.ContainsKey(fieldName))
                        ExceptionsHelper.Throw(new SyntaxError("Trying to redefinition member \"" + fieldName + "\" at " + CodeCoordinates.FromTextPosition(state.Code, s, i - s)));

                    state.CodeContext |= CodeContext.InClassDefenition;
                    state.CodeContext &= ~CodeContext.InGenerator;

                    i = s;
                    var method = FunctionDefinition.Parse(state, ref i, FunctionKind.Method) as FunctionDefinition;

                    if (fieldName == "constructor")
                    {
                        ctor = method;
                    }
                    else
                    {
                        flds[fieldName] = new MemberDescriptor(new ConstantDefinition(method.name), method, @static);
                    }
                    if (method == null)
                        ExceptionsHelper.Throw(new SyntaxError());
                }
            }
            if (ctor == null)
            {
                string ctorCode;
                int ctorIndex = 0;
                if (baseType != null && !(baseType is ConstantDefinition))
                    ctorCode = "constructor(...args) { super(...args); }";
                else
                    ctorCode = "constructor(...args) { }";
                ctor = FunctionDefinition.Parse(new ParseInfo(ctorCode, ctorCode, null)
                {
                    strict = true,
                    CodeContext = CodeContext.InClassConstructor | CodeContext.InClassDefenition
                }, ref ctorIndex, FunctionKind.Method);
            }

            var result = new ClassDefinition(name, baseType, new List<MemberDescriptor>(flds.Values).ToArray(), ctor as FunctionDefinition, computedProperties.ToArray());

            if ((state.CodeContext & CodeContext.InExpression) == 0)
            {
                if (string.IsNullOrEmpty(name))
                {
                    ExceptionsHelper.ThrowSyntaxError("Class must have name", state.Code, index);
                }
                if (state.strict && state.functionScopeLevel != state.lexicalScopeLevel)
                {
                    ExceptionsHelper.ThrowSyntaxError("In strict mode code, class can only be declared at top level or immediately within other function.", state.Code, index);
                }

                state.Variables.Add(new VariableDescriptor(result.reference, state.lexicalScopeLevel));
            }
            state.CodeContext = oldCodeContext;
            state.strict = oldStrict;
            index = i + 1;
            return result;
        }

        public override bool Build(ref CodeNode _this, int expressionDepth, Dictionary<string, VariableDescriptor> variables, CodeContext codeContext, CompilerMessageCallback message, FunctionInfo stats, Options opts)
        {
            if ((codeContext & CodeContext.InExpression) == 0)
                stats.WithLexicalEnvironment = true;

            Parser.Build(ref _constructor, expressionDepth, variables, codeContext | CodeContext.InClassDefenition | CodeContext.InClassConstructor, message, stats, opts);
            Parser.Build(ref _baseClass, expressionDepth, variables, codeContext, message, stats, opts);

            for (var i = 0; i < members.Length; i++)
            {
                Parser.Build
                (
                    ref members[i]._value,
                    expressionDepth,
                    variables,
                    codeContext | CodeContext.InClassDefenition | (members[i]._static ? CodeContext.InStaticMember : 0),
                    message,
                    stats,
                    opts
                );
            }

            for (var i = 0; i < computedProperties.Length; i++)
            {
                Parser.Build(ref computedProperties[i]._name, 2, variables, codeContext | CodeContext.InExpression, message, stats, opts);

                Parser.Build(ref computedProperties[i]._value, 2, variables, codeContext | CodeContext.InExpression, message, stats, opts);
            }

            return base.Build(ref _this, expressionDepth, variables, codeContext, message, stats, opts);
        }

        public override JSValue Evaluate(Context context)
        {
            JSValue variable = null;
            if ((_codeContext & CodeContext.InExpression) == 0)
            {
                variable = context.GetVariable(name, true);
                if (variable.Exists)
                    ExceptionsHelper.ThrowTypeError("'" + name + "' has already been declared");
                else
                    variable.attributes |= JSValueAttributesInternal.DoNotDelete;
            }

            var ctor = new ClassConstructor(context, this._constructor);
            ctor.RequireNewKeywordLevel = RequireNewKeywordLevel.WithNewOnly;

            JSValue baseProto = TypeProxy.GlobalPrototype;
            if (this._baseClass != null)
            {
                baseProto = _baseClass.Evaluate(context).oValue as JSObject;
                if (baseProto == null)
                {
                    ctor.prototype.__proto__ = null;
                }
                else
                {
                    ctor.prototype.__proto__ = Tools.InvokeGetter(baseProto.GetProperty("prototype"), baseProto).oValue as JSObject;
                }
                ctor.__proto__ = baseProto as JSObject;
            }

            for (var i = 0; i < members.Length; i++)
            {
                var member = members[i];
                var value = member.Value.Evaluate(context);
                JSValue target = null;
                if (member.Static)
                {
                    target = ctor;
                }
                else
                {
                    target = ctor.prototype;
                }

                target.SetProperty(member.Name.Evaluate(null), value, true);
            }

            for (var i = 0; i < computedProperties.Length; i++)
            {
                var member = computedProperties[i];

                JSObject target = null;
                if (member.Static)
                {
                    target = ctor;
                }
                else
                {
                    target = ctor.prototype.oValue as JSObject;
                }

                var key = member._name.Evaluate(context).CloneImpl(false);
                var value = member._value.Evaluate(context).CloneImpl(false);

                JSValue existedValue;
                Symbol symbolKey = null;
                string stringKey = null;
                if (key.Is<Symbol>())
                {
                    symbolKey = key.As<Symbol>();
                    if (target.symbols == null)
                        target.symbols = new Dictionary<Symbol, JSValue>();

                    if (!target.symbols.TryGetValue(symbolKey, out existedValue))
                        target.symbols[symbolKey] = existedValue = value;
                }
                else
                {
                    stringKey = key.As<string>();
                    if (!target.fields.TryGetValue(stringKey, out existedValue))
                        target.fields[stringKey] = existedValue = value;
                }

                if (existedValue != value)
                {
                    if (existedValue.Is(JSValueType.Property) && value.Is(JSValueType.Property))
                    {
                        var egs = existedValue.As<GsPropertyPair>();
                        var ngs = value.As<GsPropertyPair>();
                        egs.get = ngs.get ?? egs.get;
                        egs.set = ngs.set ?? egs.set;
                    }
                    else
                    {
                        if (key.Is<Symbol>())
                        {
                            target.symbols[symbolKey] = value;
                        }
                        else
                        {
                            target.fields[stringKey] = value;
                        }
                    }
                }
            }

            if ((_codeContext & CodeContext.InExpression) == 0)
                variable.Assign(ctor);
            return ctor;
        }

        public override T Visit<T>(Visitor<T> visitor)
        {
            return visitor.Visit(this);
        }

        protected internal override CodeNode[] getChildsImpl()
        {
            var result = new List<CodeNode>();

            for (var i = 0; i < members.Length; i++)
            {
                result.Add(members[i]._value);
            }

            for (var i = 0; i < computedProperties.Length; i++)
            {
                result.Add(computedProperties[i].Name);

                result.Add(computedProperties[i].Value);
            }

            return result.ToArray();
        }

        public override string ToString()
        {
            var result = new StringBuilder();
            result.Append("class ").Append(name);
            if (_baseClass != null)
                result.Append(" extends ").Append(_baseClass);
            result.Append(" {").Append(Environment.NewLine);
            for (var i = 0; i < members.Length; i++)
            {
                var t = members[i].ToString().Replace(Environment.NewLine, Environment.NewLine + "  ");
                result.Append(t);
            }
            result.Append(Environment.NewLine).Append("}");
            return result.ToString();
        }

        public override void Decompose(ref Expression self, IList<CodeNode> result)
        {
            for (var i = 0; i < members.Length; i++)
            {
                members[i]._value.Decompose(ref members[i]._value, result); // results will be empty at each iterations
#if DEBUG
                if (result.Count != 0)
                    System.Diagnostics.Debug.Fail("Decompose: results not empty");
#endif
            }

            for (var i = 0; i < computedProperties.Length; i++)
            {
                computedProperties[i]._name.Decompose(ref computedProperties[i]._name, result);

                computedProperties[i]._value.Decompose(ref computedProperties[i]._value, result);

#if DEBUG
                if (result.Count != 0)
                    System.Diagnostics.Debug.Fail("Decompose: results not empty");
#endif
            }
        }

        public override void Optimize(ref CodeNode _this, FunctionDefinition owner, CompilerMessageCallback message, Options opts, FunctionInfo stats)
        {
            for (var i = members.Length; i-- > 0;)
            {
                members[i]._value.Optimize(ref members[i]._value, owner, message, opts, stats);
            }

            for (var i = 0; i < computedProperties.Length; i++)
            {
                computedProperties[i]._name.Optimize(ref computedProperties[i]._name, owner, message, opts, stats);

                computedProperties[i]._value.Optimize(ref computedProperties[i]._value, owner, message, opts, stats);
            }
        }

        public override void RebuildScope(FunctionInfo functionInfo, Dictionary<string, VariableDescriptor> transferedVariables, int scopeBias)
        {
            base.RebuildScope(functionInfo, null, scopeBias);

            _baseClass?.RebuildScope(functionInfo, null, scopeBias);
            _constructor.RebuildScope(functionInfo, null, scopeBias);
            for (var i = 0; i < computedProperties.Length; i++)
            {
                computedProperties[i].Name.RebuildScope(functionInfo, null, scopeBias);
                computedProperties[i].Value.RebuildScope(functionInfo, null, scopeBias);
            }
            for (var i = 0; i < members.Length; i++)
            {
                members[i].Name.RebuildScope(functionInfo, null, scopeBias);
                members[i].Value.RebuildScope(functionInfo, null, scopeBias);
            }
        }
    }
}