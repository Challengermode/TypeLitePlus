﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection.Emit;
using TypeLitePlus.Extensions;
using TypeLitePlus.TsModels;

namespace TypeLitePlus
{
    /// <summary>
    /// Generates TypeScript definitions form the code model.
    /// </summary>
    public class TsGenerator
    {
        protected TsTypeFormatterCollection _typeFormatters;
        internal TypeConvertorCollection _typeConvertors;
        protected TsMemberIdentifierFormatter _memberFormatter;
        protected TsMemberTypeFormatter _memberTypeFormatter;
        protected TsTypeVisibilityFormatter _typeVisibilityFormatter;
        protected TsModuleNameFormatter _moduleNameFormatter;
        protected IDocAppender _docAppender;
        protected List<string> _references;
        private bool _isCsharp = false;

        /// <summary>
        /// Gets collection of formatters for individual TsTypes
        /// </summary>
        public IReadOnlyDictionary<Type, TsTypeFormatter> Formaters
        {
            get
            {
                return new ReadOnlyDictionary<Type, TsTypeFormatter>(_typeFormatters._formatters);
            }
        }

        /// <summary>
        /// Gets or sets string for the single indentation level.
        /// </summary>
        public string IndentationString { get; set; }

        /// <summary>
        /// Gets or sets bool value indicating whether enums should be generated as 'const enum'.
        /// Default value is null, meaning that <see cref="TsEnumAttribute.EmitConstEnum"/> will decide.
        /// </summary>
        public bool? GenerateConstEnums { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="TsGenerationModes">Mode</see> for generating the file. Defaults to <see cref="TsGenerationModes.Definitions"/>.
        /// </summary>
        public TsGenerationModes Mode { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="TsEnumModes">Mode</see> for generating the file. Defaults to <see cref="TsEnumModes.Number"/>.
        /// </summary>
        public TsEnumModes EnumMode { get; set; }

        /// <summary>
        /// Initializes a new instance of the TsGenerator class with the default formatters.
        /// </summary>
        public TsGenerator()
        {
            _references = new List<string>();

            _typeFormatters = new TsTypeFormatterCollection();
            _typeFormatters.RegisterTypeFormatter<TsClass>((type, formatter) =>
            {
                var tsClass = ((TsClass)type);
                if (!tsClass.GenericArguments.Any()) return tsClass.Name;
                return tsClass.Name + "<" + string.Join(", ", tsClass.GenericArguments.Select(a => a as TsCollection != null ? this.GetFullyQualifiedTypeName(a) + "[]" : this.GetFullyQualifiedTypeName(a))) + ">";
            });
            _typeFormatters.RegisterTypeFormatter<TsSystemType>((type, formatter) => ((TsSystemType)type).Kind.ToTypeScriptString());
            _typeFormatters.RegisterTypeFormatter<TsCollection>((type, formatter) =>
            {
                var itemType = ((TsCollection)type).ItemsType;
                if (!(itemType is TsClass itemTypeAsClass) || !itemTypeAsClass.GenericArguments.Any()) return this.GetTypeName(itemType);
                return this.GetTypeName(itemType);
            });
            _typeFormatters.RegisterTypeFormatter<TsEnum>((type, formatter) => ((TsEnum)type).Name);

            _typeConvertors = new TypeConvertorCollection();

            _docAppender = new NullDocAppender();

            _memberFormatter = DefaultMemberFormatter;
            _memberTypeFormatter = DefaultMemberTypeFormatter;
            _typeVisibilityFormatter = DefaultTypeVisibilityFormatter;
            _moduleNameFormatter = DefaultModuleNameFormatter;

            this.IndentationString = "\t";
            this.GenerateConstEnums = null;
            this.Mode = TsGenerationModes.Definitions;
            this.EnumMode = TsEnumModes.Number;
        }

        public bool DefaultTypeVisibilityFormatter(TsClass tsClass, string typeName)
        {
            //return Mode == TsGenerationModes.Definitions ? false : true;
            return true;
        }

        public string DefaultModuleNameFormatter(TsModule module, TsType _)
        {
            return module.Name;
        }

        public string DefaultMemberFormatter(TsProperty identifier)
        {
            return identifier.Name;
        }

        public string DefaultMemberTypeFormatter(TsProperty tsProperty, string memberTypeName)
        {
            var asCollection = tsProperty.PropertyType as TsCollection;
            var isCollection = asCollection != null;

            return memberTypeName + (isCollection ? string.Concat(Enumerable.Repeat("[]", asCollection.Dimension)) : "");
        }

        /// <summary>
        /// Registers the formatter for the specific TsType
        /// </summary>
        /// <typeparam name="TFor">The type to register the formatter for. TFor is restricted to TsType and derived classes.</typeparam>
        /// <param name="formatter">The formatter to register</param>
        /// <remarks>
        /// If a formatter for the type is already registered, it is overwritten with the new value.
        /// </remarks>
        public void RegisterTypeFormatter<TFor>(TsTypeFormatter formatter) where TFor : TsType
        {
            _typeFormatters.RegisterTypeFormatter<TFor>(formatter);
        }

        /// <summary>
        /// Registers the custom formatter for the TsClass type.
        /// </summary>
        /// <param name="formatter">The formatter to register.</param>
        public void RegisterTypeFormatter(TsTypeFormatter formatter)
        {
            _typeFormatters.RegisterTypeFormatter<TsClass>(formatter);
        }

        /// <summary>
        /// Registers the converter for the specific Type
        /// </summary>
        /// <typeparam name="TFor">The type to register the converter for.</typeparam>
        /// <param name="convertor">The converter to register</param>
        /// <remarks>
        /// If a converter for the type is already registered, it is overwritten with the new value.
        /// </remarks>
        public void RegisterTypeConvertor<TFor>(TypeConvertor convertor)
        {
            _typeConvertors.RegisterTypeConverter<TFor>(convertor);
        }

        /// <summary>
        /// Sets the formatter for class member identifiers.
        /// </summary>
        /// <param name="formatter">The formatter to register.</param>
        public void SetIdentifierFormatter(TsMemberIdentifierFormatter formatter)
        {
            _memberFormatter = formatter;
        }

        /// <summary>
        /// Sets the formatter for class member types.
        /// </summary>
        /// <param name="formatter">The formatter to register.</param>
        public void SetMemberTypeFormatter(TsMemberTypeFormatter formatter)
        {
            _memberTypeFormatter = formatter;
        }

        /// <summary>
        /// Sets the formatter for class member types.
        /// </summary>
        /// <param name="formatter">The formatter to register.</param>
        public void SetTypeVisibilityFormatter(TsTypeVisibilityFormatter formatter)
        {
            _typeVisibilityFormatter = formatter;
        }

        /// <summary>
        /// Sets the formatter for module names.
        /// </summary>
        /// <param name="formatter">The formatter to register.</param>
        public void SetModuleNameFormatter(TsModuleNameFormatter formatter)
        {
            _moduleNameFormatter = formatter;
        }

        /// <summary>
        /// Sets the document appender.
        /// </summary>
        /// <param name="appender">The ducument appender.</param>
        public void SetDocAppender(IDocAppender appender)
        {
            _docAppender = appender;
        }

        /// <summary>
        /// Add a typescript reference
        /// </summary>
        /// <param name="reference">Name of d.ts file used as typescript reference</param>
        public void AddReference(string reference)
        {
            _references.Add(reference);
        }

        /// <summary>
        /// Generates TypeScript definitions for properties and enums in the model.
        /// </summary>
        /// <param name="model">The code model with classes to generate definitions for.</param>
        /// <returns>TypeScript definitions for classes in the model.</returns>
        public string Generate(TsModel model)
        {
            return this.Generate(model, TsGeneratorOutput.Properties | TsGeneratorOutput.Enums);
        }

        /// <summary>
        /// Generates TypeScript definitions for classes and/or enums in the model.
        /// </summary>
        /// <param name="model">The code model with classes to generate definitions for.</param>
        /// <param name="generatorOutput">The type of definitions to generate</param>
        /// <returns>TypeScript definitions for classes and/or enums in the model..</returns>
        public string Generate(TsModel model, TsGeneratorOutput generatorOutput)
        {
            _isCsharp = (generatorOutput & TsGeneratorOutput.CSharp) == TsGeneratorOutput.CSharp;
            var sb = new ScriptBuilder(this.IndentationString);

            if (!_isCsharp
                && (generatorOutput & TsGeneratorOutput.Properties) == TsGeneratorOutput.Properties
                    || (generatorOutput & TsGeneratorOutput.Fields) == TsGeneratorOutput.Fields)
            {

                if ((generatorOutput & TsGeneratorOutput.Constants) == TsGeneratorOutput.Constants)
                {
                    // We can't generate constants together with properties or fields, because we can't set values in a .d.ts file.
                    //throw new InvalidOperationException("Cannot generate constants together with properties or fields");
                }

                foreach (var reference in _references.Concat(model.References))
                {
                    this.AppendReference(reference, sb);
                }
                sb.AppendLine();
            }

            // We can't just sort by the module name, because a formatter can jump in and change it so
            // format by the desired target name
            foreach (var module in model.Modules
                .OrderBy(c => c.SortOrder)
                .ThenBy(m => GetModuleName(m), StringComparer.InvariantCulture))
            {
                this.AppendModule(module, sb, generatorOutput);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates reference to other d.ts file and appends it to the output.
        /// </summary>
        /// <param name="reference">The reference file to generate reference for.</param>
        /// <param name="sb">The output</param>
        protected virtual void AppendReference(string reference, ScriptBuilder sb)
        {
            sb.AppendFormat("/// <reference path=\"{0}\" />", reference);
            sb.AppendLine();
        }

        protected virtual void AppendModule(TsModule module, ScriptBuilder sb, TsGeneratorOutput generatorOutput)
        {
            var classes = module.Classes
                .Where(c => !_typeConvertors.IsConvertorRegistered(c.Type) && !c.IsIgnored)
                .OrderBy(c => GetTypeName(c), StringComparer.InvariantCulture)
                .ToList();
            var baseClasses = classes
                .Where(c => c.BaseType != null)
                .Select(c => c.BaseType.Type.FullName)
                .Distinct()
                .OrderBy(c => c, StringComparer.InvariantCulture)
                .ToList();
            var enums = module.Enums
                .Where(e => !_typeConvertors.IsConvertorRegistered(e.Type) && !e.IsIgnored)
                .OrderBy(e => GetTypeName(e), StringComparer.InvariantCulture)
                .ToList();
            if ((generatorOutput == TsGeneratorOutput.Enums && enums.Count == 0) ||
                (generatorOutput == TsGeneratorOutput.Properties && classes.Count == 0) ||
                (enums.Count == 0 && classes.Count == 0))
            {
                return;
            }

            if (generatorOutput == TsGeneratorOutput.Properties && !classes.Any(c => c.Fields.Any() || c.Properties.Any()))
            {
                return;
            }

            if (generatorOutput == TsGeneratorOutput.Constants && !classes.Any(c => c.Constants.Any()))
            {
                return;
            }

            var moduleName = GetModuleName(module);
            var generateModuleHeader = moduleName != string.Empty;

            if (generateModuleHeader)
            {
                if (!_isCsharp)
                {
                    //if (generatorOutput != TsGeneratorOutput.Enums &&
                    //    (generatorOutput & TsGeneratorOutput.Constants) != TsGeneratorOutput.Constants)
                    //{
                    //    sb.Append(Mode == TsGenerationModes.Definitions ? "declare " : "export ");
                    //}
                    sb.Append("export ");

                    sb.AppendLine($"{(Mode == TsGenerationModes.Definitions ? "namespace" : "module")} {moduleName} {{");
                }
                else
                {
                    sb.AppendLine($"namespace {moduleName}");
                    sb.AppendLine("{");
                }
            }

            using (sb.IncreaseIndentation())
            {
                if ((generatorOutput & TsGeneratorOutput.Enums) == TsGeneratorOutput.Enums)
                {
                    foreach (var enumModel in enums)
                    {
                        this.AppendEnumDefinition(enumModel, sb, generatorOutput);
                    }
                }

                if (((generatorOutput & TsGeneratorOutput.Properties) == TsGeneratorOutput.Properties)
                    || (generatorOutput & TsGeneratorOutput.Fields) == TsGeneratorOutput.Fields)
                {
                    foreach (var baseClassModel in classes.Where(c => baseClasses.Contains(c.Type.FullName)))
                    {
                        this.AppendClassDefinition(baseClassModel, sb, generatorOutput);
                    }
                }

                if (((generatorOutput & TsGeneratorOutput.Properties) == TsGeneratorOutput.Properties)
                    || (generatorOutput & TsGeneratorOutput.Fields) == TsGeneratorOutput.Fields)
                {
                    foreach (var classModel in classes.Where(c => !baseClasses.Contains(c.Type.FullName)))
                    {
                        this.AppendClassDefinition(classModel, sb, generatorOutput);
                    }
                }

                if ((generatorOutput & TsGeneratorOutput.Constants) == TsGeneratorOutput.Constants
                    && !_isCsharp)
                {
                    foreach (var classModel in classes)
                    {
                        if (classModel.IsIgnored)
                        {
                            continue;
                        }

                        this.AppendConstantModule(classModel, sb);
                    }
                }
            }
            if (generateModuleHeader)
            {
                sb.AppendLine("}");
            }
        }

        /// <summary>
        /// Generates class definition and appends it to the output.
        /// </summary>
        /// <param name="classModel">The class to generate definition for.</param>
        /// <param name="sb">The output.</param>
        /// <param name="generatorOutput"></param>
        protected virtual void AppendClassDefinition(TsClass classModel, ScriptBuilder sb, TsGeneratorOutput generatorOutput)
        {
            string typeName = this.GetTypeName(classModel);
            string visibility = this.GetTypeVisibility(classModel, typeName) ? "export " : "";
            string noun = Mode == TsGenerationModes.Definitions ? "interface" : "class";
            if (_isCsharp)
            {
                visibility = "public ";
                noun = classModel.Type.IsInterface ? "interface" : "class";
            }
            _docAppender.AppendClassDoc(sb, classModel, typeName);
            sb.AppendFormatIndented("{0}{1} {2}", visibility, noun, typeName);
            if (classModel.BaseType != null)
            {
                string format = " extends {0}";
                if (_isCsharp)
                {
                    format = format.Replace("extends", ":");
                }
                sb.AppendFormat(format, this.GetFullyQualifiedTypeName(classModel.BaseType));
            }

            if (classModel.Interfaces.Count > 0)
            {
                var implementations = classModel.Interfaces.Select(GetFullyQualifiedTypeName).ToArray();

                var prefixFormat = classModel.Type.IsInterface ? " extends {0}"
                    : classModel.BaseType != null ? ", {0}"
                    : " extends {0}";
                if (_isCsharp)
                {
                    prefixFormat = prefixFormat.Replace("extends", ":");
                }

                sb.AppendFormat(prefixFormat, string.Join(", ", implementations));
            }

            if (!_isCsharp)
            {
                sb.AppendLine(" {");
            }
            else
            {
                sb.AppendLine();
                sb.AppendLineIndented("{");
            }

            var members = new List<TsProperty>();
            if (_isCsharp && !classModel.Type.IsInterface)
            {
                IEnumerable<TsType> flattenBaseInterfaces(TsClass cm)
                {
                    foreach (TsType innerInterface in cm.Interfaces)
                    {
                        yield return innerInterface;
                    }
                    if (cm.BaseType is TsClass innerBaseClassModel)
                    {
                        foreach (TsType innerInterface in flattenBaseInterfaces(innerBaseClassModel))
                        {
                            yield return innerInterface;
                        }
                    }
                }
                HashSet<TsType> baseInterfaces = new HashSet<TsType>(flattenBaseInterfaces(classModel));

                IEnumerable<ICollection<TsProperty>> flattenInterfaceProperties(TsClass cm)
                {
                    foreach (TsClass innerInterfaceModel in cm.Interfaces.Except(baseInterfaces).Cast<TsClass>())
                    {
                        foreach (ICollection<TsProperty> innerInterfaceProperties in flattenInterfaceProperties(innerInterfaceModel))
                        {
                            yield return innerInterfaceProperties;
                        }
                    }
                    if (cm != classModel)
                    {
                        yield return cm.Properties;
                    }
                }
                TsProperty[] interfaceProperties = flattenInterfaceProperties(classModel).SelectMany(a => a).ToArray();
                if (interfaceProperties.Length > 0)
                {
                    members.AddRange(interfaceProperties);
                }
            }
            if ((generatorOutput & TsGeneratorOutput.Properties) == TsGeneratorOutput.Properties)
            {
                members.AddRange(classModel.Properties);
            }
            if ((generatorOutput & TsGeneratorOutput.Fields) == TsGeneratorOutput.Fields)
            {
                members.AddRange(classModel.Fields);
            }
            using (sb.IncreaseIndentation())
            {
                if (_isCsharp)
                {
                    foreach (var property in classModel.Constants)
                    {
                        if (property.IsIgnored)
                        {
                            continue;
                        }

                        string propertyName = this.GetPropertyName(property);
                        if (propertyName == "namespace")
                        {
                            continue;
                        }

                        _docAppender.AppendPropertyDoc(sb, property, this.GetPropertyName(property), this.GetPropertyType(property));
                        sb.AppendLineIndented(string.Format("public const {1} {0} = {2};", propertyName, this.GetPropertyType(property), this.GetPropertyConstantValue(property)));
                    }
                }

                foreach (var property in members
                    .Where(p => !p.IsIgnored)
                    .OrderBy(p => this.GetPropertyName(p), StringComparer.InvariantCulture))
                {
                    _docAppender.AppendPropertyDoc(sb, property, this.GetPropertyName(property), this.GetPropertyType(property));
                    string propertyFormat = "{0}: {1};";
                    string propertyType = this.GetPropertyType(property);
                    if (_isCsharp)
                    {
                        propertyFormat = (noun == "class" ? "public " : "") + "{1} {0} {{ get; set; }}";
                        if (propertyType == "any" || propertyType == "number")
                        {
                            throw new InvalidOperationException();
                        }
                    }
                    sb.AppendLineIndented(string.Format(propertyFormat, this.GetPropertyName(property), propertyType));
                }
            }

            sb.AppendLineIndented("}");
        }

        protected virtual void AppendEnumDefinition(TsEnum enumModel, ScriptBuilder sb, TsGeneratorOutput output)
        {
            string typeName = this.GetTypeName(enumModel);
            string visibility = (output & TsGeneratorOutput.Enums) == TsGeneratorOutput.Enums || (output & TsGeneratorOutput.Constants) == TsGeneratorOutput.Constants ? "export " : "";
            if (_isCsharp)
            {
                visibility = "public ";
            }

            _docAppender.AppendEnumDoc(sb, enumModel, typeName);

            string constSpecifier = (this.GenerateConstEnums ?? enumModel.EmitConstEnum) ? "const " : string.Empty;
            string format = "{0}{2}enum {1} {{";
            if (_isCsharp)
            {
                constSpecifier = string.Empty;
                format = format.Replace(" {{", "");
                if (enumModel.Values.Any(v => long.TryParse(v.Value, out long longValue) && longValue > int.MaxValue))
                {
                    format += " : long";
                }
            }
            sb.AppendLineIndented(string.Format(format, visibility, typeName, constSpecifier));
            if (_isCsharp)
            {
                sb.AppendLineIndented("{");
            }

            using (sb.IncreaseIndentation())
            {
                int i = 1;
                foreach (var v in enumModel.Values)
                {
                    _docAppender.AppendEnumValueDoc(sb, v);
                    switch (EnumMode)
                    {
                        case TsEnumModes.String:
                            sb.AppendLineIndented($"{v.Name} = \"{v.Name}\"{(i < enumModel.Values.Count ? "," : "")}");
                            break;
                        default:
                            sb.AppendLineIndented($"{v.Name} = {v.Value}{(i < enumModel.Values.Count ? "," : "")}");
                            break;
                    }
                    i++;
                }
            }

            sb.AppendLineIndented("}");
        }

        /// <summary>
        /// Generates class definition and appends it to the output.
        /// </summary>
        /// <param name="classModel">The class to generate definition for.</param>
        /// <param name="sb">The output.</param>
        /// <param name="generatorOutput"></param>
        protected virtual void AppendConstantModule(TsClass classModel, ScriptBuilder sb)
        {
            if (!classModel.Constants.Any())
            {
                return;
            }

            string typeName = this.GetTypeName(classModel);
            sb.AppendLineIndented(string.Format("export namespace {0} {{", typeName));

            using (sb.IncreaseIndentation())
            {
                foreach (var property in classModel.Constants)
                {
                    if (property.IsIgnored)
                    {
                        continue;
                    }

                    _docAppender.AppendConstantModuleDoc(sb, property, this.GetPropertyName(property), this.GetPropertyType(property));
                    string propertyType = ": " + this.GetPropertyType(property);
                    string propertyConstantValue = this.GetPropertyConstantValue(property);
                    if (propertyConstantValue != null)
                    {
                        propertyType = string.Empty;
                    }
                    sb.AppendFormatIndented("export const {0}{1} = {2};", this.GetPropertyName(property), propertyType, propertyConstantValue);
                    sb.AppendLine();
                }

            }
            sb.AppendLineIndented("}");
        }

        /// <summary>
        /// Gets fully qualified name of the type
        /// </summary>
        /// <param name="type">The type to get name of</param>
        /// <returns>Fully qualified name of the type</returns>
        public string GetFullyQualifiedTypeName(TsType type)
        {
            var moduleName = string.Empty;

            if (type as TsModuleMember != null && !_typeConvertors.IsConvertorRegistered(type.Type))
            {
                var memberType = (TsModuleMember)type;
                moduleName = memberType.Module != null ? GetModuleName(memberType.Module) : string.Empty;
            }
            else if (type as TsCollection != null)
            {
                var collectionType = (TsCollection)type;
                moduleName = GetCollectionModuleName(collectionType, moduleName);
            }

            if (type.Type.IsGenericParameter)
            {
                return this.GetTypeName(type);
            }
            if (!string.IsNullOrEmpty(moduleName))
            {
                var name = moduleName + "." + this.GetTypeName(type);
                return name;
            }

            return this.GetTypeName(type);
        }

        /// <summary>
        /// Recursively finds the module name for the underlaying ItemsType of a TsCollection.
        /// </summary>
        /// <param name="collectionType">The TsCollection object.</param>
        /// <param name="moduleName">The module name.</param>
        /// <returns></returns>
        public string GetCollectionModuleName(TsCollection collectionType, string moduleName)
        {
            if (collectionType.ItemsType as TsModuleMember != null && !_typeConvertors.IsConvertorRegistered(collectionType.ItemsType.Type))
            {
                if (!collectionType.ItemsType.Type.IsGenericParameter)
                    moduleName = ((TsModuleMember)collectionType.ItemsType).Module != null ? GetModuleName(((TsModuleMember)collectionType.ItemsType).Module) : string.Empty;
            }
            if (collectionType.ItemsType as TsCollection != null)
            {
                moduleName = GetCollectionModuleName((TsCollection)collectionType.ItemsType, moduleName);
            }
            return moduleName;
        }

        /// <summary>
        /// Gets name of the type in the TypeScript
        /// </summary>
        /// <param name="type">The type to get name of</param>
        /// <returns>name of the type</returns>
        public string GetTypeName(TsType type)
        {
            if (_isCsharp
                && Type.GetTypeCode(type.Type) > TypeCode.DBNull)
            {
                return type.Type.Name;
            }

            if (_typeConvertors.IsConvertorRegistered(type.Type))
            {
                return _typeConvertors.ConvertType(type.Type);
            }

            return _typeFormatters.FormatType(type);
        }

        /// <summary>
        /// Gets property name in the TypeScript
        /// </summary>
        /// <param name="property">The property to get name of</param>
        /// <returns>name of the property</returns>
        public string GetPropertyName(TsProperty property)
        {
            var name = _memberFormatter(property);
            if (property.IsOptional)
            {
                name += "?";
            }

            return name;
        }

        /// <summary>
        /// Gets property type in the TypeScript
        /// </summary>
        /// <param name="property">The property to get type of</param>
        /// <returns>type of the property</returns>
        public string GetPropertyType(TsProperty property)
        {
            var fullyQualifiedTypeName = GetFullyQualifiedTypeName(property.PropertyType);
            return _memberTypeFormatter(property, fullyQualifiedTypeName);
        }

        /// <summary>
        /// Gets property constant value in TypeScript format
        /// </summary>
        /// <param name="property">The property to get constant value of</param>
        /// <returns>constant value of the property</returns>
        public string GetPropertyConstantValue(TsProperty property)
        {
            var quote = property.PropertyType.Type == typeof(string) ? "\"" : "";
            string stringValue = Convert.ToString(property.ConstantValue, CultureInfo.InvariantCulture);
            return quote + stringValue + quote;
        }

        /// <summary>
        /// Gets whether a type should be marked with "Export" keyword in TypeScript
        /// </summary>
        /// <param name="tsClass"></param>
        /// <param name="typeName">The type to get the visibility of</param>
        /// <returns>bool indicating if type should be marked weith keyword "Export"</returns>
        public bool GetTypeVisibility(TsClass tsClass, string typeName)
        {
            return _typeVisibilityFormatter(tsClass, typeName);
        }

        /// <summary>
        /// Formats a module name
        /// </summary>
        /// <param name="module">The module to be formatted</param>
        /// <returns>The module name after formatting.</returns>
        public string GetModuleName(TsModule module, TsType type = null)
        {
            return _moduleNameFormatter(module, type);
        }
    }
}
