using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Roles.DTOs;
using MyApi.Modules.Roles.Models;

namespace MyApi.Modules.Roles.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PermissionService> _logger;

        // Define all available modules and actions. MUST stay a superset of the
        // frontend PERMISSION_MODULES (src/types/permissions.ts) so that "Grant All"
        // and "Grant Module" actually cover every module the UI gates features on.
        private static readonly Dictionary<string, string[]> AvailablePermissions = new()
        {
            // CRM
            { "contacts", new[] { "create", "read", "update", "delete", "export", "import", "archive", "restore", "bulk_edit", "bulk_delete", "print" } },
            { "articles", new[] { "create", "read", "update", "delete", "export", "import", "archive", "duplicate", "bulk_edit" } },
            { "offers", new[] { "create", "read", "update", "delete", "export", "approve", "reject", "send", "print", "duplicate", "convert", "archive" } },
            { "sales", new[] { "create", "read", "update", "delete", "export", "approve", "convert", "archive", "print", "bulk_edit" } },
            { "deals", new[] { "create", "read", "update", "delete" } },
            { "purchases", new[] { "create", "read", "update", "delete" } },
            // Field Service
            { "installations", new[] { "create", "read", "update", "delete", "export", "import", "archive" } },
            { "service_orders", new[] { "create", "read", "update", "delete", "export", "assign", "approve", "archive", "print", "convert" } },
            { "dispatches", new[] { "create", "read", "update", "delete", "assign", "approve" } },
            { "dispatcher", new[] { "create", "read", "update", "delete", "assign", "manage" } },
            // Time & Expenses
            { "time_tracking", new[] { "create", "read", "update", "delete", "approve", "export", "view_all", "view_own" } },
            { "expenses", new[] { "create", "read", "update", "delete", "approve", "reject", "export", "view_all", "view_own" } },
            // Inventory
            { "stock_management", new[] { "read", "add_stock", "remove_stock", "read_logs" } },
            // Administration
            { "users", new[] { "create", "read", "update", "delete", "assign", "archive", "restore", "bulk_edit" } },
            { "roles", new[] { "create", "read", "update", "delete", "assign", "manage" } },
            { "settings", new[] { "create", "read", "update", "delete", "configure", "manage", "switch_company" } },
            { "audit_logs", new[] { "read", "export", "delete" } },
            { "documents", new[] { "read" } },
            { "dynamic_forms", new[] { "create", "read", "update", "delete" } },
            { "ai_assistant", new[] { "read" } },
            { "hr", new[] { "create", "read", "update", "delete" } },
            { "external_endpoints", new[] { "create", "read", "update", "delete" } },
            // Reporting — gate sensitive dashboards behind explicit read permissions
            { "reporting_finance", new[] { "read" } },
            { "reporting_hr", new[] { "read" } }
        };

        public PermissionService(ApplicationDbContext context, ILogger<PermissionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<RolePermissionsDto> GetRolePermissionsAsync(int roleId)
        {
            var role = await _context.Roles
                .Where(r => r.Id == roleId && r.IsActive)
                .FirstOrDefaultAsync();

            if (role == null)
                throw new ArgumentException("Role not found");

            var permissions = await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .Select(rp => new PermissionDto
                {
                    Id = rp.Id,
                    RoleId = rp.RoleId,
                    Module = rp.Module,
                    Action = rp.Action,
                    Granted = rp.Granted,
                    CreatedAt = rp.CreatedAt,
                    UpdatedAt = rp.UpdatedAt
                })
                .ToListAsync();

            return new RolePermissionsDto
            {
                RoleId = roleId,
                RoleName = role.Name,
                Permissions = permissions
            };
        }

        public async Task<RolePermissionsDto> UpdateRolePermissionsAsync(int roleId, UpdateRolePermissionsRequest request, string modifiedBy)
        {
            var role = await _context.Roles
                .Where(r => r.Id == roleId && r.IsActive)
                .FirstOrDefaultAsync();

            if (role == null)
                throw new ArgumentException("Role not found");

            var existingMap = await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .ToDictionaryAsync(rp => (rp.Module, rp.Action));

            foreach (var permRequest in request.Permissions)
            {
                if (existingMap.TryGetValue((permRequest.Module, permRequest.Action), out var existingPermission))
                {
                    existingPermission.Granted = permRequest.Granted;
                    existingPermission.UpdatedAt = DateTime.UtcNow;
                    existingPermission.ModifiedBy = modifiedBy;
                }
                else
                {
                    _context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = roleId,
                        Module = permRequest.Module,
                        Action = permRequest.Action,
                        Granted = permRequest.Granted,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = modifiedBy
                    });
                }
            }

            await _context.SaveChangesAsync();

            return await GetRolePermissionsAsync(roleId);
        }

        public async Task<PermissionDto> SetPermissionAsync(int roleId, SetPermissionRequest request, string modifiedBy)
        {
            var role = await _context.Roles
                .Where(r => r.Id == roleId && r.IsActive)
                .FirstOrDefaultAsync();

            if (role == null)
                throw new ArgumentException("Role not found");

            var existingPermission = await _context.RolePermissions
                .FirstOrDefaultAsync(rp => 
                    rp.RoleId == roleId && 
                    rp.Module == request.Module && 
                    rp.Action == request.Action);

            RolePermission permission;

            if (existingPermission != null)
            {
                existingPermission.Granted = request.Granted;
                existingPermission.UpdatedAt = DateTime.UtcNow;
                existingPermission.ModifiedBy = modifiedBy;
                permission = existingPermission;
            }
            else
            {
                permission = new RolePermission
                {
                    RoleId = roleId,
                    Module = request.Module,
                    Action = request.Action,
                    Granted = request.Granted,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = modifiedBy
                };
                _context.RolePermissions.Add(permission);
            }

            await _context.SaveChangesAsync();

            return new PermissionDto
            {
                Id = permission.Id,
                RoleId = permission.RoleId,
                Module = permission.Module,
                Action = permission.Action,
                Granted = permission.Granted,
                CreatedAt = permission.CreatedAt,
                UpdatedAt = permission.UpdatedAt
            };
        }

        public async Task<bool> DeletePermissionAsync(int roleId, string module, string action)
        {
            var permission = await _context.RolePermissions
                .FirstOrDefaultAsync(rp => 
                    rp.RoleId == roleId && 
                    rp.Module == module && 
                    rp.Action == action);

            if (permission == null)
                return false;

            _context.RolePermissions.Remove(permission);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<UserPermissionsDto> GetUserPermissionsAsync(int userId)
        {
            // Get all active role IDs for the user
            var userRoleIds = await _context.UserRoles
                .Where(ur => ur.UserId == userId && ur.IsActive)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            // Get all granted permissions for those roles
            var permissions = await _context.RolePermissions
                .Where(rp => userRoleIds.Contains(rp.RoleId) && rp.Granted)
                .Select(rp => $"{rp.Module}:{rp.Action}")
                .Distinct()
                .ToListAsync();

            return new UserPermissionsDto
            {
                UserId = userId,
                Permissions = permissions
            };
        }

        public async Task<bool> UserHasPermissionAsync(int userId, string module, string action)
        {
            // Get all active role IDs for the user
            var userRoleIds = await _context.UserRoles
                .Where(ur => ur.UserId == userId && ur.IsActive)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            if (!userRoleIds.Any())
                return false;

            // Check if any of the user's roles have the required permission
            return await _context.RolePermissions
                .AnyAsync(rp => 
                    userRoleIds.Contains(rp.RoleId) && 
                    rp.Module == module && 
                    rp.Action == action && 
                    rp.Granted);
        }

        public async Task<bool> GrantAllPermissionsAsync(int roleId, string modifiedBy)
        {
            var role = await _context.Roles
                .Where(r => r.Id == roleId && r.IsActive)
                .FirstOrDefaultAsync();

            if (role == null)
                return false;

            var existingMap = await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .ToDictionaryAsync(rp => (rp.Module, rp.Action));

            foreach (var module in AvailablePermissions)
            {
                foreach (var action in module.Value)
                {
                    if (existingMap.TryGetValue((module.Key, action), out var existing))
                    {
                        existing.Granted = true;
                        existing.UpdatedAt = DateTime.UtcNow;
                        existing.ModifiedBy = modifiedBy;
                    }
                    else
                    {
                        _context.RolePermissions.Add(new RolePermission
                        {
                            RoleId = roleId,
                            Module = module.Key,
                            Action = action,
                            Granted = true,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = modifiedBy
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RevokeAllPermissionsAsync(int roleId, string modifiedBy)
        {
            var permissions = await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .ToListAsync();

            foreach (var permission in permissions)
            {
                permission.Granted = false;
                permission.UpdatedAt = DateTime.UtcNow;
                permission.ModifiedBy = modifiedBy;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> GrantModulePermissionsAsync(int roleId, string module, string modifiedBy)
        {
            var role = await _context.Roles
                .Where(r => r.Id == roleId && r.IsActive)
                .FirstOrDefaultAsync();

            if (role == null)
                return false;

            if (!AvailablePermissions.ContainsKey(module))
                return false;

            var existingMap = await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId && rp.Module == module)
                .ToDictionaryAsync(rp => rp.Action);

            foreach (var action in AvailablePermissions[module])
            {
                if (existingMap.TryGetValue(action, out var existingPermission))
                {
                    existingPermission.Granted = true;
                    existingPermission.UpdatedAt = DateTime.UtcNow;
                    existingPermission.ModifiedBy = modifiedBy;
                }
                else
                {
                    _context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = roleId,
                        Module = module,
                        Action = action,
                        Granted = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = modifiedBy
                    });
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RevokeModulePermissionsAsync(int roleId, string module, string modifiedBy)
        {
            var permissions = await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId && rp.Module == module)
                .ToListAsync();

            foreach (var permission in permissions)
            {
                permission.Granted = false;
                permission.UpdatedAt = DateTime.UtcNow;
                permission.ModifiedBy = modifiedBy;
            }

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
