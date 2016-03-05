// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Internal.TypeSystem;
using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis
{
    public class GCStaticsNode : ObjectNode, ISymbolNode
    {
        private MetadataType _type;

        public GCStaticsNode(MetadataType type)
        {
            _type = type;
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }
        
        string ISymbolNode.MangledName
        {
            get
            {
                return "__GCStaticBase_" + NodeFactory.NameMangler.GetMangledTypeName(_type);
            }
        }

        private ISymbolNode GetGCStaticEETypeNode(NodeFactory context)
        {
            // TODO Replace with better gcDesc computation algorithm when we add gc handling to the type system
            bool[] gcDesc = new bool[_type.GCStaticFieldSize / context.Target.PointerSize + 1];
            return context.GCStaticEEType(gcDesc);
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory context)
        {
            DependencyList dependencyList = new DependencyList();
            
            if (context.TypeInitializationManager.HasEagerStaticConstructor(_type))
            {
                dependencyList.Add(context.EagerCctorIndirection(_type.GetStaticConstructor()), "Eager .cctor");
            }

            dependencyList.Add(context.GCStaticsRegion, "GCStatics Region");
            dependencyList.Add(GetGCStaticEETypeNode(context), "GCStatic EEType");
            dependencyList.Add(context.GCStaticIndirection(_type), "GC statics indirection");
            return dependencyList;
        }

        public override bool ShouldShareNodeAcrossModules(NodeFactory factory)
        {
            return factory.CompilationModuleGroup.ShouldShareAcrossModules(_type);
        }

        int ISymbolNode.Offset
        {
            get
            {
                return 0;
            }
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        public override ObjectNodeSection Section
        {
            get
            {
                return ObjectNodeSection.DataSection;
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory);

            builder.RequirePointerAlignment();
            builder.EmitPointerReloc(GetGCStaticEETypeNode(factory), 1);
            builder.DefinedSymbols.Add(this);

            return builder.ToObjectData();
        }
    }
}
