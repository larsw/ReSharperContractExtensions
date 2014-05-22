﻿using System.Diagnostics.Contracts;
using System.Linq;
using JetBrains.Application.Progress;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.CSharp.Bulbs;
using JetBrains.ReSharper.Feature.Services.CSharp.Generate;
using JetBrains.ReSharper.Feature.Services.Generate;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;

namespace ReSharper.ContractExtensions.ContextActions.ContractsFor
{
    internal sealed class AddContractForExecutor
    {
        private readonly AddContractForAvailability _addContractForAvailability;
        private readonly ICSharpContextActionDataProvider _provider;
        private readonly CSharpElementFactory _factory;
        private readonly ICSharpFile _currentFile;

        public AddContractForExecutor(AddContractForAvailability addContractForAvailability,
            ICSharpContextActionDataProvider provider)
        {
            Contract.Requires(addContractForAvailability != null);
            Contract.Requires(addContractForAvailability.IsAvailable);
            Contract.Requires(provider != null);

            _addContractForAvailability = addContractForAvailability;
            _provider = provider;

            _factory = _provider.ElementFactory;
            // TODO: look at this class CSharpStatementNavigator

            Contract.Assert(provider.SelectedElement != null);
            
            _currentFile = (ICSharpFile)provider.SelectedElement.GetContainingFile();
        }

        public void Execute(ISolution solution, IProgressIndicator progress)
        {
            string contractClassName = CreateContractClassName();

            AddContractClassAttribute(contractClassName);

            IClassDeclaration contractClass = GenerateContractClassDeclaration(contractClassName);

            AddContractClassForAttributeTo(contractClass);

            var physicalContractClassDeclaration = 
                AddToPhysicalDeclarationAfter(contractClass);

            ImplementInterfaceOrBaseClass(physicalContractClassDeclaration);
        }

        private void AddContractClassForAttributeTo(IClassDeclaration contractClass)
        {
            var attribute = CreateContractClassForAttribute(
                _addContractForAvailability.TypeDeclaration.DeclaredName);
            contractClass.AddAttributeAfter(attribute, null);
        }

        private void ImplementContractForAbstractClass(IClassDeclaration contractClass,
            IClassDeclaration abstractClass)
        {
            Contract.Requires(contractClass != null);
            Contract.Requires(contractClass.DeclaredElement != null);
            Contract.Requires(abstractClass != null);
            Contract.Requires(abstractClass.DeclaredElement != null);

            using (var workflow = GeneratorWorkflowFactory.CreateWorkflowWithoutTextControl(
                GeneratorStandardKinds.Overrides,
                contractClass,
                abstractClass))
            {
                Contract.Assert(workflow != null);

                // By default this input for this workflow contains too many members:
                // It contains member from the base class (required) and
                // members from the other base classes (i.e. from System.Object).
                // Using some hack to get only members defined in the "abstractClass"

                // So I'm trying to find required elements myself.

                var membersInOrder =
                    GenerateUtil.GetOverridableMembersOrder(contractClass.DeclaredElement, false)
                        .Where(e => e.DeclaringType.GetClrName().FullName ==
                                    abstractClass.DeclaredElement.GetClrName().FullName)
                        .Select(x => x)
                        .Select(x => new GeneratorDeclaredElement<IOverridableMember>(x.Member, x.Substitution))
                        // This code provides two elements for property! So I'm trying to remove another instance!
                        .Where(x => !x.DeclaredElement.ShortName.StartsWith("get_"))
                        .ToList();

                workflow.Context.InputElements.Clear();

                workflow.Context.ProvidedElements.Clear();
                workflow.Context.ProvidedElements.AddRange(membersInOrder);

                workflow.Context.InputElements.AddRange(workflow.Context.ProvidedElements);

                workflow.GenerateAndFinish("Generate contract class", NullProgressIndicator.Instance);
            }
        }

        private void ImplementContractForInterface(IClassDeclaration contractClass,
            IInterfaceDeclaration interfaceDeclaration)
        {
            Contract.Requires(contractClass != null);
            Contract.Requires(interfaceDeclaration != null);

            if (interfaceDeclaration.MemberDeclarations.Count == 0)
                return;

            using (var workflow = GeneratorWorkflowFactory.CreateWorkflowWithoutTextControl(
                GeneratorStandardKinds.Implementations,
                contractClass,
                interfaceDeclaration))
            {
                Contract.Assert(workflow != null);

                workflow.Context.SetGlobalOptionValue(
                    CSharpBuilderOptions.ImplementationKind,
                    CSharpBuilderOptions.ImplementationKindExplicit);

                workflow.Context.InputElements.Clear();

                workflow.Context.InputElements.AddRange(workflow.Context.ProvidedElements);

                workflow.GenerateAndFinish("Generate contract class", NullProgressIndicator.Instance);
            }
        }

        private void ImplementInterfaceOrBaseClass(IClassDeclaration contractClass)
        {
            if (_addContractForAvailability.IsAbstractClass)
            {
                ImplementContractForAbstractClass(contractClass, _addContractForAvailability.ClassDeclaration);
            }
            else
            {
                ImplementContractForInterface(contractClass, _addContractForAvailability.InterfaceDeclaration);
            }
        }

        /// <summary>
        /// Adds <paramref name="contractClass"/> after implemented interface into the physical tree.
        /// </summary>
        /// <remarks>
        /// This method is absolutely crucial, because all R# "generate workflows" works correcntly
        /// only if TreeNode.IsPhysical() returns true.
        /// </remarks>
        private IClassDeclaration AddToPhysicalDeclarationAfter(IClassDeclaration contractClass)
        {
            var holder = CSharpTypeAndNamespaceHolderDeclarationNavigator.GetByTypeDeclaration(
                _addContractForAvailability.TypeDeclaration);
            Contract.Assert(holder != null);

            var physicalContractClassDeclaration =
                (IClassDeclaration) holder.AddTypeDeclarationAfter(contractClass,
                    _addContractForAvailability.TypeDeclaration);

            return physicalContractClassDeclaration;
        }

        private IClassDeclaration GenerateContractClassDeclaration(string contractClassName)
        {
            Contract.Requires(contractClassName != null);
            Contract.Ensures(Contract.Result<IClassDeclaration>() != null);

            var classDeclaration = (IClassDeclaration)_factory.CreateTypeMemberDeclaration(
                string.Format("abstract class {0} : $0 {{}}", contractClassName), 
                _addContractForAvailability.DeclaredType);

            return classDeclaration;
        }

        [Pure]
        private string CreateContractClassName()
        {
            return _addContractForAvailability.TypeDeclaration.DeclaredName + "Contract";
        }

        // TODO: controlflow said that IAttribute is not the best way to do this!
        [Pure]
        private IAttribute CreateContractClassAttribute(string contractClassName)
        {
            ITypeElement type = TypeFactory.CreateTypeByCLRName(
                typeof(ContractClassAttribute).FullName,
                _provider.PsiModule, _currentFile.GetResolveContext()).GetTypeElement();

            var expression = _factory.CreateExpressionAsIs(
                string.Format("typeof({0})", contractClassName));

            var attribute = _factory.CreateAttribute(type);

            attribute.AddArgumentAfter(
                _factory.CreateArgument(ParameterKind.VALUE, expression),
                null);

            return attribute;
        }

        [Pure]
        private IAttribute CreateContractClassForAttribute(string contractClassForName)
        {
            ITypeElement type = TypeFactory.CreateTypeByCLRName(
                typeof(ContractClassForAttribute).FullName,
                _provider.PsiModule, _currentFile.GetResolveContext()).GetTypeElement();

            var expression = _factory.CreateExpressionAsIs(
                string.Format("typeof({0})", contractClassForName));

            var attribute = _factory.CreateAttribute(type);

            attribute.AddArgumentAfter(
                _factory.CreateArgument(ParameterKind.VALUE, expression),
                null);

            return attribute;
        }

        private void AddContractClassAttribute(string contractClassName)
        {
            var attribute = CreateContractClassAttribute(contractClassName);
            _addContractForAvailability.TypeDeclaration.AddAttributeAfter(attribute, null);
        }
    }
}