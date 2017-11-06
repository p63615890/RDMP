﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CatalogueLibrary.Data.DataLoad;
using CatalogueManager.ItemActivation;
using ReusableLibraryCode.CommandExecution;
using ReusableUIComponents.CommandExecution;
using ReusableUIComponents.CommandExecution.Proposals;

namespace CatalogueManager.CommandExecution.Proposals
{
    public abstract class RDMPCommandExecutionProposal<T>:ICommandExecutionProposal where T : class
    {
        protected readonly IActivateItems ItemActivator;

        protected RDMPCommandExecutionProposal(IActivateItems itemActivator)
        {
            ItemActivator = itemActivator;
        }

        public abstract bool CanActivate(T target);
        public abstract void Activate(T target);

        public abstract ICommandExecution ProposeExecution(ICommand cmd, T target, InsertOption insertOption = InsertOption.Default);

        public bool IsCompatibleTarget(object target)
        {
            return target is T && IsCompatibleTargetImpl((T)target);
        }

        protected virtual bool IsCompatibleTargetImpl(T target)
        {
            return true;
        }

        public ICommandExecution ProposeExecution(ICommand cmd, object target,InsertOption insertOption = InsertOption.Default)
        {
            if(IsCompatibleTarget(target))
                return ProposeExecution(cmd,(T)target, insertOption);

            return null;
        }

        public void Activate(object target)
        {
            if (IsCompatibleTarget(target))
                Activate((T) target);
        }

        public bool CanActivate(object target)
        {
            return IsCompatibleTarget(target) && CanActivate((T)target);
        }
    }
}
