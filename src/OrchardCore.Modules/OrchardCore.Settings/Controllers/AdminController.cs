using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Newtonsoft.Json;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Localization.Services;
using OrchardCore.Settings.ViewModels;

namespace OrchardCore.Settings.Controllers
{
    public class AdminController : Controller, IUpdateModel
    {
        private readonly IDisplayManager<ISite> _siteSettingsDisplayManager;
        private readonly ISiteService _siteService;
        private readonly INotifier _notifier;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICultureManager _cultureManager;

        public AdminController(
            ISiteService siteService,
            IDisplayManager<ISite> siteSettingsDisplayManager,
            IAuthorizationService authorizationService,
            INotifier notifier,
            IHtmlLocalizer<AdminController> h,
            ICultureManager cultureManager)
        {
            _siteSettingsDisplayManager = siteSettingsDisplayManager;
            _siteService = siteService;
            _notifier = notifier;
            _authorizationService = authorizationService;
            H = h;
            _cultureManager = cultureManager;
        }

        IHtmlLocalizer H { get; set; }

        public async Task<IActionResult> Index(string groupId)
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageGroupSettings, (object)groupId))
            {
                return Unauthorized();
            }

            var site = await _siteService.GetSiteSettingsAsync();

            var viewModel = new AdminIndexViewModel
            {
                GroupId = groupId,
                Shape = await _siteSettingsDisplayManager.BuildEditorAsync(site, this, false, groupId)
            };

            return View(viewModel);
        }

        [HttpPost]
        [ActionName(nameof(Index))]
        public async Task<IActionResult> IndexPost(string groupId)
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageGroupSettings, (object)groupId))
            {
                return Unauthorized();
            }

            var cachedSite = await _siteService.GetSiteSettingsAsync();

            // Clone the settings as the driver will update it and as it's a globally cached object
            // it would stay this way even on validation errors.

            var site = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(cachedSite, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }), cachedSite.GetType()) as ISite;

            var viewModel = new AdminIndexViewModel
            {
                GroupId = groupId,
                Shape = await _siteSettingsDisplayManager.UpdateEditorAsync(site, this, false, groupId)
            };

            if (ModelState.IsValid)
            {
                await _siteService.UpdateSiteSettingsAsync(site);

                _notifier.Success(H["Site settings updated successfully."]);

                return RedirectToAction(nameof(Index), new { groupId });
            }

            return View(viewModel);
        }

        public async Task<IActionResult> Culture()
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageSettings))
            {
                return Unauthorized();
            }

            var model = new SiteCulturesViewModel
            {
                CurrentCulture = _cultureManager.GetCurrentCulture(),
                SiteCultures = _cultureManager.ListCultures().Select(x => x.CultureName)
            };

            model.AvailableSystemCultures = CultureInfo.GetCultures(CultureTypes.AllCultures).Where(c => c.Name != String.Empty)
                .Select(ci => CultureInfo.GetCultureInfo(ci.Name))
                .Where(s => !model.SiteCultures.Contains(s.Name));

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> AddCulture(string systemCultureName, string cultureName)
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageSettings))
            {
                return Unauthorized();
            }

            cultureName = String.IsNullOrWhiteSpace(cultureName) ? systemCultureName : cultureName;

            if (!String.IsNullOrWhiteSpace(cultureName) && _cultureManager.IsValidCulture(cultureName))
            {
                _cultureManager.AddCulture(cultureName);
            }
            return RedirectToAction("Culture");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCulture(string cultureName)
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageSettings))
            {
                return Unauthorized();
            }

            _cultureManager.DeleteCulture(cultureName);
            return RedirectToAction("Culture");
        }
    }
}