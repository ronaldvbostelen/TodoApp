using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TodoApp.DB;
using TodoApp.DB.Models;

namespace TodoApp.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Route("api/[controller]")]
    [ApiController]
    public class TodoController : ControllerBase
    {
        private readonly TodoContext _context;
        private readonly ILogger<TodoContext> _logger;
        private readonly HttpContext _httpContext;

        public TodoController(TodoContext context, ILogger<TodoContext> logger, IHttpContextAccessor contextAccessor)
        {
            _context = context;
            _logger = logger;
            _httpContext = contextAccessor.HttpContext;
        }

        // GET: api/Todo
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ItemData>>> GetItems()
        {
            _logger.LogInformation($"Items fetched, from: {_httpContext.Connection.RemoteIpAddress}");
            return await _context.Items.ToListAsync();
        }

        // GET: api/Todo/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ItemData>> GetItemData(int id)
        {
            _logger.LogInformation($"Item #{id} fetched, from: {_httpContext.Connection.RemoteIpAddress}");

            var itemData = await _context.Items.FindAsync(id);

            if (itemData == null)
            {
                return NotFound();
            }

            return itemData;
        }

        // PUT: api/Todo/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutItemData(int id, ItemData itemData)
        {
            if (id != itemData.Id)
            {
                _logger.LogWarning($"Putrequest with faulty data url id #{id}, from: {_httpContext.Connection.RemoteIpAddress}");
                return BadRequest();
            }
            
            var dbRecord = await _context.Items.FindAsync(id);
            
            if (dbRecord == null)
            {
                _logger.LogWarning($"Tried updating (put) non-existing item #{id}, from: {_httpContext.Connection.RemoteIpAddress}");
                return NotFound();
            }

            dbRecord.Title = itemData.Title;
            dbRecord.Details = itemData.Details;
            dbRecord.Completed = itemData.Completed;
            
            _context.Entry(itemData).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Item #{id} put, from: {_httpContext.Connection.RemoteIpAddress}");
            }
            catch (DbUpdateConcurrencyException e)
            {
                _logger.LogError(e.Message);
                
                if (!ItemDataExists(id))
                {
                    return NotFound();
                }
                
                throw;
            }

            return NoContent();
        }

        // POST: api/Todo
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<ItemData>> PostItemData(ItemData itemData)
        {
            if (ModelState.IsValid)
            {
                await _context.Items.AddAsync(itemData);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Item #{itemData.Id} added, from: {_httpContext.Connection.RemoteIpAddress}");

                return CreatedAtAction("GetItemData", new { id = itemData.Id }, itemData);
            }
            
            return new JsonResult("Something went wrong") {StatusCode = 500};

        }

        // DELETE: api/Todo/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteItemData(int id)
        {
            var itemData = await _context.Items.FindAsync(id);
            if (itemData == null)
            {
                _logger.LogWarning($"Tried deleting non-existing item #{id}, from: {_httpContext.Connection.RemoteIpAddress}");
                return NotFound();
            }

            _context.Items.Remove(itemData);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation($"Item #{id} deleted, from: {_httpContext.Connection.RemoteIpAddress}");

            return NoContent();
        }

        private bool ItemDataExists(int id)
        {
            return _context.Items.Any(e => e.Id == id);
        }
    }
}
