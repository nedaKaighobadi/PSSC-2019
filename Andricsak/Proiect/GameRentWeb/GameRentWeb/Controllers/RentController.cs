﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using GameRentWeb.Models;
using GameRentWeb.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace GameRentWeb.Controllers
{
    public class RentController : Controller
    {
        private readonly IDataBaseRepo<RentOrder> _rentOrders;
        private readonly IDataBaseRepo<Game> _games;
        private readonly IDataBaseRepo<User> _users;
        private ICollection<RentOrder> _rents;
        private readonly MessageBroker _broker;
        private static Game gameRented;
        
        private int rentedGameid;
        public RentController(IDataBaseRepo<RentOrder> rentOrders, IDataBaseRepo<Game> games, MessageBroker broker,
            IDataBaseRepo<User> users)
        {
            _rentOrders = rentOrders;
            _games = games;
            _broker = broker;
            _users = users;
            _rents = new Collection<RentOrder>();
        }

        public IActionResult DisplayRents()
        {
            if (HttpContext.Session.GetString("Username") == null)
            {
                TempData["Error"] = "You need to login First!";
                return RedirectToAction("Index", "Game");
            }
            var loggedUser = _users.GetAllObjects().Result.FirstOrDefault(u => u.UserName.Equals(HttpContext.Session.GetString("Username")));
            
            List<RentOrder> rents = _rentOrders.GetAllObjects().Result.Where(r => r.user.Id == loggedUser.Id).ToList();
            return View("MyRents", rents);
        }
        
        [HttpGet]
        public IActionResult Index(int id)
        {
            if(HttpContext.Session.GetString("Username") ==null)
            {
                TempData["Error"] = "You need to login First!";
                return RedirectToAction("Index","Game");
            }
            
            rentedGameid = id;
            gameRented = _games.GetObjectById(rentedGameid).Result;
            ViewBag.GameName = gameRented.Name;
            if(gameRented.Quantity < 1)
            {
                TempData["Error"] = "There are no copies available!";
                return RedirectToAction("Index", "Game");
            }
            return View();
        }

        public async Task<IActionResult> Rent(RentOrder rent)
        {
            rent.CurrentRentedDay = DateTime.Today;
            rent.GameRented = gameRented.Name;
           
            var rentJson = JsonConvert.SerializeObject(rent);
            await _broker.SendMessage(rentJson,"RentToWorker");
            var rentReceived = _broker.ReceiveMessage("WorkerToRent").Result;

            rent.ExpiringDate = rentReceived.ExpiringDate;
            rent.TotalPayment = rentReceived.TotalPayment;

            var user = _users.GetAllObjects().Result.FirstOrDefault(u => u.UserName.Equals(HttpContext.Session.GetString("Username"))); 

            user.RentOrders = new Collection<RentOrder>();
            
            if(user.Balance > rent.TotalPayment && gameRented.Quantity >= 1)
            {
                user.Balance -= rent.TotalPayment;
                // update game quantity
                gameRented.Quantity -= 1;
                await _rentOrders.Insert(rent);
                user.RentOrders.Add(rent);
                await _games.Update(gameRented);
                
                await _users.Update(user);
                HttpContext.Session.SetString("Balance", Convert.ToString(user.Balance));
                return RedirectToAction("Index", "Game");
            }
            else
            {
                TempData["Funds"] = $"Not enough funds, payment is {rent.TotalPayment}$!";
                return View("Index");
            }
            
        }

        public async Task<IActionResult> Return(int id)
        {
            RentOrder selectedRent = _rentOrders.GetObjectById(id).Result;

        }

        public async Task<IActionResult> Extend(int id)
        {
            RentOrder selectedRent =  _rentOrders.GetObjectById(id).Result;


        }
    }
}