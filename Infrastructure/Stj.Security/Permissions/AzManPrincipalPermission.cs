﻿#region Using

using System;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using System.Security.Principal;
using System.Threading;
using Stj.Security.Principal;

#endregion Using

namespace Stj.Security.Permissions
{
    public class AzManPrincipalPermission : IPermission, ISecurityEncodable, IUnrestrictedPermission
    {

        #region Members

        private bool _isUnrestricted = false;

        #endregion Members

        #region Properties

        public bool IsAuthenticated { get; private set; }
        public string[] RequiredOperations { get; private set; }
        public string[] RequiredTasks { get; private set; }

        #endregion Properties

        #region Constructors

        internal AzManPrincipalPermission(bool isAuthenticated, string[] requiredOperations, string[] requiredTasks) {
            IsAuthenticated = isAuthenticated;
            RequiredOperations = requiredOperations;
            RequiredTasks = requiredTasks;
        }

        public AzManPrincipalPermission(string requiredOperation, string requiredTask) : this(true, new string[] { requiredOperation }, new string[] { requiredTask }) { }

        public AzManPrincipalPermission(PermissionState state)
        {
            _isUnrestricted = (state == PermissionState.Unrestricted);
        }

        #endregion Constructors

        #region Methods

        #region IPermission

        public void Demand()
        {
            var principal = Thread.CurrentPrincipal;
            if (principal != null)
            {
                CheckAuthentication(principal);

                if (principal is AzManPrincipal)
                {
                    CheckOperationsOrTasks((AzManPrincipal)principal);
                }
            }
            else
            {
                throw new SecurityException(Resources.MessagePermissionAccessDeniedInvalidPrincipal);
            }
        }

        private void CheckAuthentication(IPrincipal principal)
        {
            if (IsAuthenticated && (principal.Identity.IsAuthenticated == false))
            {
                throw new SecurityException(Resources.MessagePermissionAccessDeniedUserNotAuthenticated);
            }
        }

        private void CheckOperationsOrTasks(AzManPrincipal principal)
        {
            if (RequiredOperations == null)  return;

            if (!(principal.HasRequiredOperations(RequiredOperations) || principal.HasRequiredTasks(RequiredTasks)))
            {
                throw new SecurityException(Resources.MessagePermissionAccessDeniedUserCannotAccess);
            }
        }

        public IPermission Copy()
        {
            if (IsUnrestricted())
            {
                return new AzManPrincipalPermission(PermissionState.Unrestricted);
            }
            else
            {
                return new AzManPrincipalPermission(IsAuthenticated, RequiredOperations, RequiredTasks);
            }
        }

        public IPermission Intersect(IPermission target)
        {
            if (target == null) return null;
            var permission = target as AzManPrincipalPermission;
            if (permission == null) return null;
            if (IsUnrestricted()) return target.Copy();
            if (permission.IsUnrestricted()) return Copy();
            if (IsAuthenticated != permission.IsAuthenticated) return null;

            // TODO: validate/test Intersect()
            //var operations = RequiredOperations.Where(operation => permission.RequiredOperations.Contains(operation)).ToArray();
            var operations = RequiredOperations.Intersect(permission.RequiredOperations).ToArray();
            var tasks = RequiredTasks.Intersect(permission.RequiredTasks).ToArray();

            //foreach (var operation in RequiredOperations)
            //{
            //    if (permission.RequiredOperations.Contains(operation))
            //    {
            //        operations.Add(operation);
            //    }
            //}

            return new AzManPrincipalPermission(IsAuthenticated, operations.ToArray(), tasks.ToArray());
        }

        public bool IsSubsetOf(IPermission target)
        {
            if (target == null) return false;
            var permission = target as AzManPrincipalPermission;
            if (permission == null) return false;
            if (permission.IsUnrestricted()) return true;
            if (IsUnrestricted()) return false;
            if (IsAuthenticated != permission.IsAuthenticated) return false;

            var isSubsetOf = true;
            foreach (var operation in RequiredOperations)
            {
                if (permission.RequiredOperations.Contains(operation) == false)
                {
                    isSubsetOf = false;
                    break;
                }
            }

            return isSubsetOf;
        }

        public IPermission Union(IPermission target)
        {
            if (target == null) return null;
            var permission = target as AzManPrincipalPermission;
            if (permission == null) return null;
            if (permission.IsUnrestricted() || IsUnrestricted()) return new AzManPrincipalPermission(PermissionState.Unrestricted);
            if (IsAuthenticated != permission.IsAuthenticated) return null;

            var operations = RequiredOperations.Union(permission.RequiredOperations).ToArray();
            var tasks = RequiredTasks.Union(permission.RequiredTasks).ToArray();

            // TODO: validate/test Union()
            //List<Claim> claims = new List<Claim>();
            //foreach (Claim c in m_requiredClaims)
            //    claims.Add(c);

            //foreach (Claim c in perm.RequiredClaims)
            //{
            //    if (!m_requiredClaims.ContainsClaim(c))
            //    {
            //        claims.Add(c);
            //    }
            //}

            return new AzManPrincipalPermission(IsAuthenticated, operations, tasks);
        }

        #endregion IPermission

        #region ISecurityEncodable

        public void FromXml(SecurityElement e)
        {
            throw new NotImplementedException();
        }

        public SecurityElement ToXml()
        {
            throw new NotImplementedException();
        }

        #endregion ISecurityEncodable Methods

        #region IUnrestrictedPermission Methods

        public bool IsUnrestricted()
        {
            return _isUnrestricted;
        }

        #endregion IUnrestrictedPermission

        #endregion Methods

    }
}
