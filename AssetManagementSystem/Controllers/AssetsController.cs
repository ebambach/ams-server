﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using AssetManagementSystem.Models;
using Utility;

namespace AssetManagementSystem.Controllers
{
    public class AssetsController : Controller
    {
        private AssetManagementSystemContext db = new AssetManagementSystemContext();

        /// <summary>
        /// HTTP POST Add asset to the database
        /// </summary>
        /// <param name="asset">JSON Asset object from HTTP Body</param>
        /// <returns>JSON Msg</returns>
        public ActionResult Add([System.Web.Http.FromBody] Asset asset)
        {
            if (asset == null || asset.Name == null)
            {
                return Json(new Msg { Result = "Error", Message = "Asset.Add(): Asset null." }, JsonRequestBehavior.AllowGet);
            }

            // Stop entity framework from adding anything to location table
            asset.Location = null;

            Vehicle v = null;
            if (asset.Vehicle != null)
                v = asset.Vehicle;

            // if the ModelState is valid (no existing errors)
            if (ModelState.IsValid)
            {
                db.Assets.Add(asset); // add our asset
                int numChanges = 0;
                try
                {
                    numChanges = db.SaveChanges();  // try to save changes
                }
                catch (Exception e)
                {
                    // we blew up, return the exception to the front end
                    return new JsonNetResult { Data = e.GetBaseException().Message };
                }

                //v.Asset = db.Assets.Find(asset.Id);
                //v.AssetId = v.Asset.Id;
                //db.Vehicles.Add(v);
                //db.SaveChanges();

                // if we got here, return success
                return Json(new Msg { Result = "Success", Message = $"Asset.Add(): {numChanges} record(s) changed." }, JsonRequestBehavior.AllowGet);
            }

            // ModelState is invalid
            return Json(new Msg { Result = "Error", Message = "Asset.Add(): ModelState invalid." }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// HTTP POST Change an existing asset in the database
        /// </summary>
        /// <param name="asset">JSON Asset Object from HTTP Body</param>
        /// <returns>JSON Msg</returns>
        public ActionResult Change([System.Web.Http.FromBody] Asset asset)
        {
            // do we have a valid asset?
            if (asset == null)
            {
                // nope; tell the user
                return Json(new Msg { Result = "Error", Message = "Asset.Change(): asset cannot be null." }, JsonRequestBehavior.AllowGet);
            }

            // stop entity framework from adding to location table
            asset.Location = null;

            // try to find the asset to update
            Asset dbAsset = db.Assets.Find(asset.Id);

            // did we find an asset?
            if (dbAsset == null)
            {
                // no; tell the user
                return Json(new Msg { Result = "Error", Message = $"Asset.Change(): invalid asset id {asset.Id}." }, JsonRequestBehavior.AllowGet);
            }

            // yes; update it
            dbAsset.Copy(asset);

            int numChanges = 0; // number of records changed on db.SaveChanges()
            if (ModelState.IsValid)
            {
                try
                {
                    numChanges = db.SaveChanges();  // try to save
                }
                catch (Exception e)
                {
                    // something blew up; pass it forward
                    return new JsonNetResult { Data = e.GetBaseException().Message };
                }

                // everything seems to have gone well
                return Json(new Msg { Result = "Success", Message = $"Asset.Change(): {numChanges} record(s) updated." }, JsonRequestBehavior.AllowGet);
            }

            // the ModelState is invalid
            return Json(new Msg { Result = "Error", Message = "Asset.Change(): ModelState invalid." }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// HTTP GET Gets asset from the database
        /// </summary>
        /// <param name="id">Asset.Id from URL</param>
        /// <returns>JSON Asset Object or error message</returns>
        public ActionResult Get(int? id)
        {
            // do we have an id and is it valid?
            if (id == null || id <= 0)
            {
                // no; tell the user
                return Json(new Msg { Result = "Error", Message = "Asset.Get(): Asset.Id null or zero." }, JsonRequestBehavior.AllowGet);
            }

            // try and find an asset
            Asset asset = db.Assets.Find(id);

            // did we find an asset?
            if (asset == null)
            {
                // no; tell the user
                return Json(new Msg { Result = "Error", Message = "Asset.Get(): Invalid Asset.Id." }, JsonRequestBehavior.AllowGet);
            }

            // delete the circular reference
            if (asset.Vehicle != null)
                asset.Vehicle.Asset = null;

            // we have an asset; return it
            return new JsonNetResult { Data = asset };
        }

        /// <summary>
        /// HTTP GET List all assets in the database
        /// </summary>
        /// <returns>JSON List of Asset objects</returns>
        public ActionResult List()
        {
            // get all assets from the database and ship them out
            List<Asset> assets = db.Assets.ToList();

            // delete any circular references
            foreach(Asset asset in assets)
            {
                // if there's a vehicle attached
                if (asset.Vehicle != null)
                    asset.Vehicle.Asset = null; // make sure it doesn't reference this asset anymore
            }

            return new JsonNetResult { Data = assets };
        }

        /// <summary>
        /// HTTP GET Remove asset from the database
        /// </summary>
        /// <param name="id">Asset.Id from URL</param>
        /// <returns>JSON Msg</returns>
        public ActionResult Remove(int? id)
        {
            // did we get a valid id?
            if (id == null || id <= 0)
            {
                // no; tell the user
                return Json(new Msg { Result = "Error", Message = "Asset.Remove(): Asset.Id null or invalid." }, JsonRequestBehavior.AllowGet);
            }

            // try and find an asset with the provided Id
            Asset asset = db.Assets.Find(id);

            // did we find one?
            if (asset == null)
            {
                // no asset found
                return Json(new Msg { Result = "Error", Message = "Asset.Remove(): invalid Asset.Id." }, JsonRequestBehavior.AllowGet);
            }

            // Delete the asset
            db.Assets.Remove(asset);

            int numChanges = 0; // number of records changed
            try
            {
                // try to save changes
                numChanges = db.SaveChanges();
            }
            catch (Exception e)
            {
                // something blew up; ship the exception off
                return new JsonNetResult { Data = e.GetBaseException().Message };
            }

            // asset deleted
            return Json(new Msg { Result = "Success", Message = $"Asset.Remove(): {numChanges} record(s) deleted." }, JsonRequestBehavior.AllowGet);
        }

        // search
        public ActionResult Search(string licenseplate, int? location)
        {
            bool PreviousSearch = false;    // whether or not we've previously searched on a term
            List<Asset> assets = new List<Asset>(); // all the assets we find

            // were we given a licenseplate?
            if (licenseplate != null && licenseplate != "")
            {
                PreviousSearch = true;  // we will have done a search
                // find all the vehicles with the given licenseplate
                List<Vehicle> vehicles = db.Vehicles.Where(v => v.License.ToLower().Contains(licenseplate.ToLower())).ToList();

                // if we found something
                if (vehicles != null && vehicles.Count > 0)
                {
                    // add the whole asset to the list
                    foreach (Vehicle vehicle in vehicles)
                    {
                        if (vehicle.Asset != null)
                            assets.Add(vehicle.Asset);
                    }
                }
            }

            // were we given a location?
            if (location != null)
            {
                if (PreviousSearch)  // did we search for anything yet?
                {
                    // search only in previous results
                    assets = assets.Where(a => a.LocationId == location).ToList();
                }
                else // search from all assets
                {
                    PreviousSearch = true;  // we will have done a search
                    // find/add all assets with provided location
                    assets.AddRange(db.Assets.Where(a => a.LocationId == location).ToList());
                }
            }

            foreach (Asset asset in assets)
            {
                if (asset.Vehicle != null)
                    asset.Vehicle.Asset = null;
            }

            // return whatever we've found
            return new JsonNetResult { Data = assets.Distinct() };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
