using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using FoodSupply.Data;
using FoodSupply.Models;

namespace FoodSupply.Controllers
{
    [Authorize(Roles = "Main Admin")]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Users
        public async Task<IActionResult> Index()
        {
            var users = await _context.Users
                .Where(u => !u.IsArchived)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return View(users);
        }

        // GET: Users/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User user, string password)
        {
            // Remove PasswordHash validation because the password
            // comes from the separate "password" form field.
            ModelState.Remove(
                nameof(FoodSupply.Models.User.PasswordHash)
            );

            // Password is required
            if (string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(
                    "password",
                    "Password is required."
                );
            }

            if (!ModelState.IsValid)
            {
                return View(user);
            }

            // Check duplicate email
            var emailExists = await _context.Users
                .AnyAsync(u => u.Email == user.Email);

            if (emailExists)
            {
                ModelState.AddModelError(
                    "Email",
                    "Email already exists."
                );

                return View(user);
            }

            // Check duplicate username
            var usernameExists = await _context.Users
                .AnyAsync(u => u.Username == user.Username);

            if (usernameExists)
            {
                ModelState.AddModelError(
                    "Username",
                    "Username already exists."
                );

                return View(user);
            }

            // Temporary password storage.
            // Replace with secure password hashing when login is implemented.
            user.PasswordHash = password;

            user.IsActive = true;
            user.IsArchived = false;
            user.CreatedAt = DateTime.Now;

            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            TempData["Success"] = "User created successfully.";

            return RedirectToAction(nameof(Index));
        }

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, User user)
        {
            if (id != user.Id)
            {
                return NotFound();
            }

            // PasswordHash is not being edited from the Edit form.
            ModelState.Remove(
                nameof(FoodSupply.Models.User.PasswordHash)
            );

            if (!ModelState.IsValid)
            {
                return View(user);
            }

            try
            {
                var existingUser = await _context.Users
                    .FindAsync(id);

                if (existingUser == null)
                {
                    return NotFound();
                }

                existingUser.FullName = user.FullName;
                existingUser.Email = user.Email;
                existingUser.Username = user.Username;
                existingUser.Role = user.Role;
                existingUser.IsActive = user.IsActive;

                await _context.SaveChangesAsync();

                TempData["Success"] = "User updated successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Users.AnyAsync(u => u.Id == id))
                {
                    return NotFound();
                }

                throw;
            }
        }

        // POST: Users/Archive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            user.IsArchived = true;
            user.IsActive = false;

            await _context.SaveChangesAsync();

            TempData["Success"] = "User archived successfully.";

            return RedirectToAction(nameof(Index));
        }

        // GET: Users/Archived
        public async Task<IActionResult> Archived()
        {
            var users = await _context.Users
                .Where(u => u.IsArchived)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return View(users);
        }

        // POST: Users/Restore/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            user.IsArchived = false;
            user.IsActive = true;

            await _context.SaveChangesAsync();

            TempData["Success"] = "User restored successfully.";

            return RedirectToAction(nameof(Archived));
        }
    }
}