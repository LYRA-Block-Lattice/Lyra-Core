﻿using Akka.Util;
using Loyc.Collections;
using Lyra.Core.API;
using Lyra.Core.Decentralize;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;

namespace Noded
{
    /// <summary>
    /// web api for Lyra Web3 eCommerce
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ECController : ControllerBase
    {
        //private readonly TodoContext _context;

        //public Web3Controller(TodoContext context) =>
        //    _context = context;

        public ECController()
        {

        }

        /// <summary>
        /// Find last service hash
        /// </summary>
        /// <returns>Hash</returns>
        [Route("ServiceHash")]
        [HttpGet]
        [ApiExplorerSettings(GroupName = "v2")]
        public async Task<IActionResult> LastServiceHashAsync()
        {
            var blk = await NodeService.Dag.Storage.GetLastServiceBlockAsync();
            if (blk == null)
            {
                return NotFound("Block not found.");
            }
            return Ok(blk.Hash);
        }

        /// <summary>
        /// Find block by hash
        /// </summary>
        /// <param name="hash">the SHA256 hash of block</param>
        /// <returns>Json serialized block</returns>
        [Route("Block")]
        [HttpGet]
        [ApiExplorerSettings(GroupName = "v2")]
        public async Task<IActionResult> FindBlockByHashAsync(string hash)
        {
            var blk = await NodeService.Dag.Storage.FindBlockByHashAsync(hash);
            if (blk == null)
            {
                return NotFound("Block not found.");
            }
            return Ok(blk);
        }

        /// <summary>
        /// find order for catalog.
        /// </summary>
        /// <param name="catalog"></param>
        /// <returns></returns>
        [Route("Orders")]
        [HttpGet]
        [ApiExplorerSettings(GroupName = "v2")]
        public async Task<IActionResult> FindTradableUniOrdersAsync(string? catalog)
        {
            var blks = await NodeService.Dag.Storage.FindTradableUniOrders2Async(catalog);
            if (blks == null)
            {
                return NotFound($"Orders not found for catalog {catalog}.");
            }

            var result = JsonConvert.SerializeObject(BsonTypeMapper.MapToDotNetValue(blks));
            return Content(result, "application/json");
            //return Ok(blks);
        }

        /// <summary>
        /// get order by id
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        [Route("Order")]
        [HttpGet]
        [ApiExplorerSettings(GroupName = "v2")]
        public async Task<IActionResult> GetUniOrderByIdAsync(string orderId)
        {
            var blks = await NodeService.Dag.Storage.GetUniOrderByIdAsync(orderId);
            if (blks == null)
            {
                return NotFound($"Order not found for id {orderId}.");
            }

            var result = JsonConvert.SerializeObject(blks);
            return Content(result, "application/json");
        }

        [Route("Balance")]
        [HttpGet]
        [ApiExplorerSettings(GroupName = "v2")]
        public async Task<IActionResult> GetBalanceAsync(string accountId)
        {
            var blks = await NodeService.Dag.Storage.GetBalanceAsync(accountId);
            if (blks == null || blks.Count == 0)
            {
                return NotFound($"Balance not found for id {accountId}.");
            }

            var objs = blks.Select(a => BsonTypeMapper.MapToDotNetValue(a))
                .ToList();
            return Content(JsonConvert.SerializeObject(objs), "application/json");
        }

        ////GET: api/Node/5
        //[ApiExplorerSettings(GroupName = "v2")]
        //[HttpGet("{id}", Name = "Get")]
        //public string Get(int id)
        //{
        //    return "value";
        //}

        //[HttpGet]
        //public async Task<List<TodoItem>> Get() =>
        //    await _context.TodoItems.ToListAsync();

        //[HttpGet("{id}")]
        //public async Task<ActionResult<TodoItem>> Get(long id)
        //{
        //    var item = await _context.TodoItems.FindAsync(id);

        //    if (item is null)
        //    {
        //        return NotFound();
        //    }

        //    return item;
        //}

        /// <summary>
        /// Creates a TodoItem.
        /// </summary>
        /// <param name="item"></param>
        /// <returns>A newly created TodoItem</returns>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /Todo
        ///     {
        ///        "id": 1,
        ///        "name": "Item #1",
        ///        "isComplete": true
        ///     }
        ///
        /// </remarks>
        /// <response code="201">Returns the newly created item</response>
        /// <response code="400">If the item is null</response>
        //[HttpPost]
        //[ProducesResponseType(StatusCodes.Status201Created)]
        //[ProducesResponseType(StatusCodes.Status400BadRequest)]
        //public async Task<IActionResult> Create(TodoItem item)
        //{
        //    _context.TodoItems.Add(item);
        //    await _context.SaveChangesAsync();

        //    return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
        //}

        /// <summary>
        /// Deletes a specific TodoItem.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        //[HttpDelete("{id}")]
        //public async Task<IActionResult> Delete(long id)
        //{
        //    var item = await _context.TodoItems.FindAsync(id);

        //    if (item is null)
        //    {
        //        return NotFound();
        //    }

        //    _context.TodoItems.Remove(item);
        //    await _context.SaveChangesAsync();

        //    return NoContent();
        //}
    }
}