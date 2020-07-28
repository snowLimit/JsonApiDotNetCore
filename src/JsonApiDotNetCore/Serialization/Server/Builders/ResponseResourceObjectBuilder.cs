using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JsonApiDotNetCore.Extensions;
using JsonApiDotNetCore.Internal.Contracts;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Query;
using JsonApiDotNetCore.Serialization.Server.Builders;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCore.Serialization.Server {
    public class ResponseResourceObjectBuilder : ResourceObjectBuilder {
        private readonly IIncludedResourceObjectBuilder _includedBuilder;
        private readonly IIncludeService _includeService;
        private readonly ILinkBuilder _linkBuilder;
        private readonly IFieldsToSerialize _fieldsToSerialize;
        private RelationshipAttribute _requestRelationship;
        private readonly ILogger<ResponseResourceObjectBuilder> _logger;

        public ResponseResourceObjectBuilder(IFieldsToSerialize fieldsToSerialize,
                                             ILinkBuilder linkBuilder,
                                             IIncludedResourceObjectBuilder includedBuilder,
                                             IIncludeService includeService,
                                             IResourceContextProvider provider,
                                             IResourceObjectBuilderSettingsProvider settingsProvider,
                                             ILoggerFactory loggerFactory)
            : base(provider, settingsProvider.Get()) {
            _logger = loggerFactory.CreateLogger<ResponseResourceObjectBuilder>();
            _linkBuilder = linkBuilder;
            _includedBuilder = includedBuilder;
            _includeService = includeService;
            _fieldsToSerialize = fieldsToSerialize;
        }

        public RelationshipEntry Build(IIdentifiable entity, RelationshipAttribute requestRelationship) {
            _requestRelationship = requestRelationship;
            return GetRelationshipData(requestRelationship, entity);
        }

        /// <summary>
        /// Builds the values of the relationships object on a resource object.
        /// The server serializer only populates the "data" member when the relationship is included,
        /// and adds links unless these are turned off. This means that if a relationship is not included
        /// and links are turned off, the entry would be completely empty, ie { }, which is not conform
        /// json:api spec. In that case we return null which will omit the entry from the output.
        /// </summary>
        protected override RelationshipEntry GetRelationshipData(RelationshipAttribute relationship, IIdentifiable entity) {
            RelationshipEntry relationshipEntry = null;
            List<List<RelationshipAttribute>> relationshipChains = null;

            // just to set the basic relationship-data array
            relationshipEntry = base.GetRelationshipData(relationship, entity);

            // login params
            LogParams(relationship, entity, relationshipEntry);

            var pi = relationship.PropertyInfo;

            // check if collection. (HasManyAttribute) string also counts as collection so 
            if (pi.IsNonStringEnumerable()) {
                _logger.LogWarning("-- is a collection");

                LogProperties(entity);
                LogAllowedRelationships(entity);

              
                //relationshipChains = new List<List<RelationshipAttribute>> { allowedRelations };
                //_includedBuilder.GetIncluded().Add(allowedRelations[0]);

                var hasManyAttribute = (HasManyAttribute)relationship;
                _logger.LogInformation("-- public HasManyName: {0}", hasManyAttribute.PublicRelationshipName);
                _logger.LogWarning("-- does this work? {0}", hasManyAttribute);

                //relationshipEntry.ManyData = new List<ResourceIdentifierObject> {
                //    new ResourceIdentifierObject { Id = "sven", Type = "sven" },
                //    new ResourceIdentifierObject { Id = "sven", Type = "sven" },
                //};

            } else { // only for single toOne
                LogPropertyInfos(pi);

                var id = entity.GetPropValue(string.Concat(pi.PropertyType.Name, "Id"));
                relationshipEntry.Data = new ResourceIdentifierObject {
                    Id = id.ToString(),
                    Type = relationship.PublicRelationshipName
                };
            }


            ShouldInclude(relationship, out relationshipChains);
            if (true || Equals(relationship, _requestRelationship) || ShouldInclude(relationship, out relationshipChains)) {
                if (true || (relationshipChains != null && relationshipEntry.HasResource)) {
                    LogAfterIfCheck(relationshipEntry, relationshipChains);

                    // just cheating
                    var allowedRelations = _fieldsToSerialize.GetAllowedRelationships(entity.GetType());
                    relationshipChains = new List<List<RelationshipAttribute>>();
                    relationshipChains.Add(allowedRelations);
                    _logger.LogWarning("-- relationshipChains.Count {0}", relationshipChains.Count);

                    var fieldsRelations = _fieldsToSerialize.GetAllowedRelationships(entity.GetType());
                    _includedBuilder.IncludeRelationshipChain(fieldsRelations, entity);
                    //foreach (var chain in relationshipChains) {
                    //    LogChainAndChildren(relationshipChains, chain);

                    //    foreach (var item in chain) {
                    //        _logger.LogCritical("-- item in Chain Properties");
                    //        LogProperties(item);
                    //    }
                    //    var fieldsRelations = _fieldsToSerialize.GetAllowedRelationships(entity.GetType());
                    //    foreach (var item in fieldsRelations) {
                    //        _logger.LogCritical("-- item in Allowed Properties");
                    //        LogProperties(item);
                    //    }
                    //    var isEqual = chain.First().Equals(fieldsRelations.First());
                    //    _logger.LogError("isEqual? {0}", isEqual);


                    //    // traverses (recursively) and extracts all (nested) related entities for the current inclusion chain.
                    //    _includedBuilder.IncludeRelationshipChain(fieldsRelations, entity);
                    //}
                }
            }

            var attrs = _includedBuilder.GetIncluded()
                  .First().Attributes
                  .Select(x => $"{x.Key} - {x.Value}").Join("\n");
            _logger.LogInformation("\nAttributes of included:\n{0}\n", attrs);

            var links = _linkBuilder.GetRelationshipLinks(relationship, entity);
            if (links != null)
                // if links relationshipLinks should be built for this entry, populate the "links" field.
                (relationshipEntry ??= new RelationshipEntry()).Links = links;

            _includedBuilder.RemoveAllIncluded();

            // if neither "links" nor "data" was populated, return null, which will omit this entry from the output.
            // (see the NullValueHandling settings on <see cref="ResourceObject"/>)
            return relationshipEntry;
        }

        private void LogChainAndChildren(List<List<RelationshipAttribute>> relationshipChains, List<RelationshipAttribute> chain) {
            _logger.LogWarning("-- chain ({0}) in relationship: {1}", relationshipChains.IndexOf(chain), chain);

            foreach (var item in chain) {
                _logger.LogWarning("-- item ({0}) in chain: {1}", chain.IndexOf(item), item);
            }
        }

        private void LogAfterIfCheck(RelationshipEntry relationshipEntry, List<List<RelationshipAttribute>> relationshipChains) {
            _logger.LogWarning("-- passed the if check: {0}, relationship != null: {1}, hasResource: {2}",
                                    relationshipChains != null && relationshipEntry.HasResource, relationshipChains != null,
                                    relationshipEntry.HasResource);
        }

        private void LogPropertyInfos(PropertyInfo pi) {
            _logger.LogInformation("-- propInfo: {0}", pi);
            _logger.LogInformation("-- propInfoName: {0}", pi.Name);
            _logger.LogInformation("-- propInfoType: {0}", pi.PropertyType);
            _logger.LogInformation("-- propInfoTypeName: {0}", pi.PropertyType.Name);
        }

        private void LogAllowedRelationships(IIdentifiable entity) {
            var allowedRelations = _fieldsToSerialize.GetAllowedRelationships(entity.GetType());
            var relOutput = allowedRelations.Select(x => x.PublicRelationshipName).Join("\n");
            _logger.LogInformation("\n{0}\n", relOutput);
        }

        private void LogProperties(object entity) {
            var output = entity.GetType().GetProperties()
                                .Select(x => $"{x.Name} - {entity.GetPropValue(x.Name)}")
                                .Join("\n");
            _logger.LogInformation("\n{0}\n", output);
        }

        private void LogParams(RelationshipAttribute relationship, IIdentifiable entity, RelationshipEntry relationshipEntry) {
            _logger.LogCritical("-- GetRelationshipData()");
            _logger.LogWarning("-- \nParams: \n relationship: {0}\n entity: {1}\n _requestRelationship: {2}\n relationshipEntry: {3}",
                relationship, entity, _requestRelationship, relationshipEntry);
        }

        /// <summary>
        /// Inspects the included relationship chains (see <see cref="IIncludeService"/>
        /// to see if <paramref name="relationship"/> should be included or not.
        /// </summary>
        private bool ShouldInclude(RelationshipAttribute relationship, out List<List<RelationshipAttribute>> inclusionChain) {
            inclusionChain = _includeService.Get()?.Where(l => l.First().Equals(relationship)).ToList();
            return inclusionChain != null && inclusionChain.Any();
        }
    }

    static class MyExtensions {

        public static string FirstCharToUpper(this string input) {
            if (string.IsNullOrEmpty(input))
                throw new ArgumentException("ARGH!");
            return input.First().ToString().ToUpper() + input.Substring(1);
        }

        public static bool IsNonStringEnumerable(this PropertyInfo pi) {
            return pi != null && pi.PropertyType.IsNonStringEnumerable();
        }

        public static bool IsNonStringEnumerable(this object instance) {
            return instance != null && instance.GetType().IsNonStringEnumerable();
        }

        public static bool IsNonStringEnumerable(this Type type) {
            if (type == null || type == typeof(string))
                return false;
            return typeof(IEnumerable).IsAssignableFrom(type);
        }

        public static object GetPropValue(this object obj, string name) {
            foreach (string part in name.Split('.')) {
                if (obj == null) { return null; }

                Type type = obj.GetType();
                PropertyInfo info = type.GetProperty(part);
                if (info == null) { return null; }

                obj = info.GetValue(obj, null);
            }
            return obj;
        }

        public static T GetPropValue<T>(this object obj, string name) {
            object retval = GetPropValue(obj, name);
            if (retval == null) { return default; }

            // throws InvalidCastException if types are incompatible
            return (T)retval;
        }
    }
}
