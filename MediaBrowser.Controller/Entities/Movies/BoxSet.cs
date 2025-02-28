#nullable disable

#pragma warning disable CA1721, CA1819, CS1591

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Controller.Entities.Movies
{
    /// <summary>
    /// Class BoxSet.
    /// </summary>
    public class BoxSet : Folder, IHasTrailers, IHasDisplayOrder, IHasLookupInfo<BoxSetInfo>
    {
        public BoxSet()
        {
            DisplayOrder = ItemSortBy.PremiereDate;
        }

        [JsonIgnore]
        protected override bool FilterLinkedChildrenPerUser => true;

        [JsonIgnore]
        public override bool SupportsInheritedParentImages => false;

        [JsonIgnore]
        public override bool SupportsPeople => true;

        /// <inheritdoc />
        public IReadOnlyList<BaseItem> LocalTrailers => GetExtras()
            .Where(extra => extra.ExtraType == Model.Entities.ExtraType.Trailer)
            .ToArray();

        /// <summary>
        /// Gets or sets the display order.
        /// </summary>
        /// <value>The display order.</value>
        public string DisplayOrder { get; set; }

        [JsonIgnore]
        private bool IsLegacyBoxSet
        {
            get
            {
                if (string.IsNullOrEmpty(Path))
                {
                    return false;
                }

                if (LinkedChildren.Length > 0)
                {
                    return false;
                }

                return !FileSystem.ContainsSubPath(ConfigurationManager.ApplicationPaths.DataPath, Path);
            }
        }

        [JsonIgnore]
        public override bool IsPreSorted => true;

        public Guid[] LibraryFolderIds { get; set; }

        protected override bool GetBlockUnratedValue(User user)
        {
            return user.GetPreferenceValues<UnratedItem>(PreferenceKind.BlockUnratedItems).Contains(UnratedItem.Movie);
        }

        public override double GetDefaultPrimaryImageAspectRatio()
            => 2.0 / 3;

        public override UnratedItem GetBlockUnratedType()
        {
            return UnratedItem.Movie;
        }

        protected override IEnumerable<BaseItem> GetNonCachedChildren(IDirectoryService directoryService)
        {
            if (IsLegacyBoxSet)
            {
                return base.GetNonCachedChildren(directoryService);
            }

            return Enumerable.Empty<BaseItem>();
        }

        protected override List<BaseItem> LoadChildren()
        {
            if (IsLegacyBoxSet)
            {
                return base.LoadChildren();
            }

            // Save a trip to the database
            return new List<BaseItem>();
        }

        public override bool IsAuthorizedToDelete(User user, List<Folder> allCollectionFolders)
        {
            return true;
        }

        public override bool IsSaveLocalMetadataEnabled()
        {
            return true;
        }

        public override List<BaseItem> GetChildren(User user, bool includeLinkedChildren, InternalItemsQuery query)
        {
            var children = base.GetChildren(user, includeLinkedChildren, query);

            if (string.Equals(DisplayOrder, ItemSortBy.SortName, StringComparison.OrdinalIgnoreCase))
            {
                // Sort by name
                return LibraryManager.Sort(children, user, new[] { ItemSortBy.SortName }, SortOrder.Ascending).ToList();
            }

            if (string.Equals(DisplayOrder, ItemSortBy.PremiereDate, StringComparison.OrdinalIgnoreCase))
            {
                // Sort by release date
                return LibraryManager.Sort(children, user, new[] { ItemSortBy.ProductionYear, ItemSortBy.PremiereDate, ItemSortBy.SortName }, SortOrder.Ascending).ToList();
            }

            // Default sorting
            return LibraryManager.Sort(children, user, new[] { ItemSortBy.ProductionYear, ItemSortBy.PremiereDate, ItemSortBy.SortName }, SortOrder.Ascending).ToList();
        }

        public override IEnumerable<BaseItem> GetRecursiveChildren(User user, InternalItemsQuery query)
        {
            var children = base.GetRecursiveChildren(user, query);

            if (string.Equals(DisplayOrder, ItemSortBy.PremiereDate, StringComparison.OrdinalIgnoreCase))
            {
                // Sort by release date
                return LibraryManager.Sort(children, user, new[] { ItemSortBy.ProductionYear, ItemSortBy.PremiereDate, ItemSortBy.SortName }, SortOrder.Ascending).ToList();
            }

            return children;
        }

        public BoxSetInfo GetLookupInfo()
        {
            return GetItemLookupInfo<BoxSetInfo>();
        }

        public override bool IsVisible(User user)
        {
            if (IsLegacyBoxSet)
            {
                return base.IsVisible(user);
            }

            if (base.IsVisible(user))
            {
                if (LinkedChildren.Length == 0)
                {
                    return true;
                }

                var userLibraryFolderIds = GetLibraryFolderIds(user);
                var libraryFolderIds = LibraryFolderIds ?? GetLibraryFolderIds();

                if (libraryFolderIds.Length == 0)
                {
                    return true;
                }

                return userLibraryFolderIds.Any(i => libraryFolderIds.Contains(i));
            }

            return false;
        }

        public override bool IsVisibleStandalone(User user)
        {
            if (IsLegacyBoxSet)
            {
                return base.IsVisibleStandalone(user);
            }

            return IsVisible(user);
        }

        private Guid[] GetLibraryFolderIds(User user)
        {
            return LibraryManager.GetUserRootFolder().GetChildren(user, true)
                .Select(i => i.Id)
                .ToArray();
        }

        public Guid[] GetLibraryFolderIds()
        {
            var expandedFolders = new List<Guid>();

            return FlattenItems(this, expandedFolders)
                .SelectMany(i => LibraryManager.GetCollectionFolders(i))
                .Select(i => i.Id)
                .Distinct()
                .ToArray();
        }

        private IEnumerable<BaseItem> FlattenItems(IEnumerable<BaseItem> items, List<Guid> expandedFolders)
        {
            return items
                .SelectMany(i => FlattenItems(i, expandedFolders));
        }

        private IEnumerable<BaseItem> FlattenItems(BaseItem item, List<Guid> expandedFolders)
        {
            if (item is BoxSet boxset)
            {
                if (!expandedFolders.Contains(item.Id))
                {
                    expandedFolders.Add(item.Id);

                    return FlattenItems(boxset.GetLinkedChildren(), expandedFolders);
                }

                return Array.Empty<BaseItem>();
            }

            return new[] { item };
        }
    }
}
