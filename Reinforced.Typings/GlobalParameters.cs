using System;
using System.Linq;
using System.Reflection;
using Reinforced.Typings.Attributes;
using Reinforced.Typings.Exceptions;
using Reinforced.Typings.ReferencesInspection;

namespace Reinforced.Typings
{
    /// <summary>
    /// Collections of global TS generation parameters
    /// </summary>
    public class GlobalParameters
    {
        private bool _isLocked;
        
        private readonly TsGlobalAttribute _attr;

        internal GlobalParameters(Assembly[] sourceAssemblies)
        {
            var tsGlobal = sourceAssemblies.Select(c => c.GetCustomAttribute<TsGlobalAttribute>())
                .Where(c => c != null)
                .OrderByDescending(c => c.Priority)
                .FirstOrDefault();
            _attr = tsGlobal ?? new TsGlobalAttribute();
        }

        /// <summary>
        ///     Boolean parameter that controls writing of "auto-generated warning" comment to each generated file.
        /// It meant the comment like "// This code was generated blah blah blah..."
        /// 'true' (default) to write warning comment about auto-generated to every file.
        /// 'false' to do not.
        /// </summary>
        public bool WriteWarningComment
        {
            get { return _attr == null ? true : _attr.WriteWarningComment; }
            set
            {
                if (_isLocked) return;
                _attr.WriteWarningComment = value;
            }
        }

        /// <summary>
        ///     Specifies root namespace for hierarchical export.
        ///     Helps to avoid creating redundant directories when hierarchical export.
        /// </summary>
        public string RootNamespace
        {
            get { return _attr.RootNamespace; }
            set
            {
                if (_isLocked) return;
                _attr.RootNamespace = value;
            }
        }

        /// <summary>
        ///     Use camelCase for methods naming
        /// </summary>
        public bool CamelCaseForMethods
        {
            get { return _attr.CamelCaseForMethods; }
            set
            {
                if (_isLocked) return;
                _attr.CamelCaseForMethods = value;
            }
        }

        /// <summary>
        ///     Use camelCase for properties naming
        /// </summary>
        public bool CamelCaseForProperties
        {
            get { return _attr.CamelCaseForProperties; }
            set
            {
                if (_isLocked) return;
                _attr.CamelCaseForProperties = value;
            }
        }

        /// <summary>
        ///     Enables or disables documentation generator
        /// </summary>
        public bool GenerateDocumentation
        {
            get { return _attr.GenerateDocumentation; }
            set
            {
                if (_isLocked) return;
                _attr.GenerateDocumentation = value;
            }
        }

        /// <summary>
        /// Converts inline XML documentation tags to JSDoc: see/seealso cref to {@link X} when X is an
        /// exported type and to plain text otherwise; paramref, typeparamref and see langword to their
        /// names; c and code to backtick code. Default false (tags are copied as they are).
        /// </summary>
        public bool ConvertDocumentationTags
        {
            get { return _attr.ConvertDocumentationTags; }
            set
            {
                if (_isLocked) return;
                _attr.ConvertDocumentationTags = value;
            }
        }

        /// <summary>
        ///    Gets or sets whether all nullable properties must be exported as optional
        /// </summary>
        public bool AutoOptionalProperties
        {
            get { return _attr.AutoOptionalProperties; }
            set
            {
                if (_isLocked) return;
                _attr.AutoOptionalProperties = value;
            }
        }

        ///// <summary>
        /////     Enables or disables documentation generator
        ///// </summary>
        //public bool StrictNullChecks
        //{
        //    get { return _strictNullChecks; }
        //    set
        //    {
        //        if (_isLocked) return;
        //        _strictNullChecks = value;
        //    }
        //}

        internal void Lock()
        {
            _isLocked = true;
        }

        internal void Unlock()
        {
            _isLocked = false;
        }

        /// <summary>
        /// Specifies symbol used for tabulation
        /// </summary>
        public string TabSymbol
        {
            get { return _attr.TabSymbol; }
            set
            {
                if (_isLocked) return;
                _attr.TabSymbol = value;
            }
        }

        /// <summary>
        /// Specifies string used as the line terminator.
        /// </summary>
        public string NewLine {
            get { return _attr.NewLine; }
            set {
                if(_isLocked) return;
                _attr.NewLine = value;
            }
        }

        /// <summary>
        /// Switches RT to using TS modules system (--module) parameter and import references
        /// </summary>
        public bool UseModules
        {
            get { return _attr.UseModules; }
            set
            {
                if (_isLocked) return;
                _attr.UseModules = value;
            }
        }

        /// <summary>
        /// When true, RT will ignore classes' namespaces when arraging classes and interfaces among files. 
        /// This parameter only makes difference when using (--module)
        /// </summary>
        public bool DiscardNamespacesWhenUsingModules
        {
            get { return _attr.DiscardNamespacesWhenUsingModules; }
            set
            {
                if (_isLocked) return;
                _attr.DiscardNamespacesWhenUsingModules = value;
            }
        }


        /// <summary>
        ///     If true, export will be performed in .d.ts manner (only typings, declare module etc).
        ///     Otherwise, export will be performed to regular .ts file
        /// </summary>
        public bool ExportPureTypings
        {
            get { return _attr.ExportPureTypings; }
            set
            {
                if (_isLocked) return;
                _attr.ExportPureTypings = value;
            }
        }

        /// <summary>
        /// Gets or sets type of <see cref="Reinforced.Typings.ReferencesInspection.ReferenceProcessorBase"/> to be used while exporting files
        /// </summary>
        public Type ReferencesProcessorType
        {
            get { return _attr.ReferenceProcessorType; }
            set
            {
                if (_isLocked) return;
                if (!typeof(ReferenceProcessorBase)._IsAssignableFrom(value))
                {
                    ErrorMessages.RTE0016_InvalidRefProcessorType.Throw(value);
                }
                _attr.ReferenceProcessorType = value;
            }
        }

        /// <summary>
        ///     Gets or sets whether it is needed to sort exported members alphabetically
        /// </summary>
        public bool ReorderMembers
        {
            get { return _attr.ReorderMembers; }
            set
            {
                if (_isLocked) return;
                _attr.ReorderMembers = value;
            }
        }
        /// <summary>
        /// Gets or sets type of AST visitor that will be used to write code to output.
        /// Visitor has to be child class of <see cref="Reinforced.Typings.Visitors.TextExportingVisitor"/>
        /// </summary>
        public Type VisitorType
        {
            get { return _attr.VisitorType; }
            set
            {
                if (_isLocked) return;
                _attr.VisitorType = value;
            }
        }

        /// <summary>
        /// Gets or sets whether unresolved types must be exported as 'unknown' instead of 'any'
        /// </summary>
        public bool UnresolvedToUnknown
        {
            get { return _attr.UnresolvedToUnknown; }
            set
            {
                if (_isLocked) return;
                _attr.UnresolvedToUnknown = value;
            }
        }

        /// <summary>
        /// Emit "namespace X {" instead of the legacy "module X {" (TypeScript 6 and 7 reject the latter with TS1540). Default false.
        /// </summary>
        public bool UseNamespaceKeyword
        {
            get { return _attr.UseNamespaceKeyword; }
            set
            {
                if (_isLocked) return;
                _attr.UseNamespaceKeyword = value;
            }
        }

        /// <summary>
        /// Gets or sets whether RT must automatically treat methods returning Task as async methods
        /// </summary>
        public bool AutoAsync
        {
            get { return _attr.AutoAsync; }
            set
            {
                if (_isLocked) return;
                _attr.AutoAsync = value;
            }
        }

        /// <summary>
        /// Honour System.Text.Json attributes (JsonPropertyName, JsonIgnore, JsonPropertyOrder, JsonStringEnumConverter, JsonStringEnumMemberName, JsonPolymorphic/JsonDerivedType). Attributes are detected by full name, so no System.Text.Json reference is needed. Default false.
        /// </summary>
        public bool UseSystemTextJson
        {
            get { return _attr.UseSystemTextJson; }
            set
            {
                if (_isLocked) return;
                _attr.UseSystemTextJson = value;
            }
        }

        /// <summary>
        /// Property naming policy that mirrors System.Text.Json's JsonNamingPolicy. Applies to properties without an explicit name and takes precedence over CamelCaseForProperties. Default None.
        /// </summary>
        public JsonNamingPolicyKind JsonNamingPolicy
        {
            get { return _attr.JsonNamingPolicy; }
            set
            {
                if (_isLocked) return;
                _attr.JsonNamingPolicy = value;
            }
        }

        /// <summary>
        /// Emit "import type" for imports generated for types exported as interfaces (module mode). Default false.
        /// </summary>
        public bool UseTypeImports
        {
            get { return _attr.UseTypeImports; }
            set
            {
                if (_isLocked) return;
                _attr.UseTypeImports = value;
            }
        }

        /// <summary>
        /// Emit enums as union types of their values (string or number literals) instead of TypeScript enums. Default false.
        /// </summary>
        public bool EnumsAsUnionTypes
        {
            get { return _attr.EnumsAsUnionTypes; }
            set
            {
                if (_isLocked) return;
                _attr.EnumsAsUnionTypes = value;
            }
        }

        /// <summary>
        /// Emit exported properties and fields as readonly. Default false.
        /// </summary>
        public bool ReadonlyProperties
        {
            get { return _attr.ReadonlyProperties; }
            set
            {
                if (_isLocked) return;
                _attr.ReadonlyProperties = value;
            }
        }

        /// <summary>
        /// Distinguish optional from nullable: nullable types are emitted as "T | null" and stay required; "?" is reserved for members that may be absent (forced optional, or JsonIgnore WhenWritingNull/WhenWritingDefault). Takes precedence over AutoOptionalProperties for nullability. Default false.
        /// </summary>
        public bool StrictNullability
        {
            get { return _attr.StrictNullability; }
            set
            {
                if (_isLocked) return;
                _attr.StrictNullability = value;
            }
        }
    }
}
