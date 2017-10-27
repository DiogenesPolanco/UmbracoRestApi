using System;
using System.Collections.Generic;
using System.Linq;
using ClientDependency.Core;
using Moq;
using Umbraco.Core.Models;
using Umbraco.Core.PropertyEditors;

namespace Umbraco.RestApi.Tests.TestHelpers
{
    public class ModelMocks
    {
        [PropertyEditor("simple", "Simple", "STRING", "simple")]
        public class SimplePropertyEditor : PropertyEditor
        {
            
        }

        public static IContent SimpleMockedContent(int id = 123, int parentId = 456)
        {
            var c = Mock.Of<IContent>(
                content => content.Id == id
                           && content.Published == true
                           && content.CreateDate == DateTime.Now.AddDays(-2)
                           && content.CreatorId == 0
                           && content.HasIdentity == true
                           && content.ContentType == Mock.Of<IContentType>(ct => ct.Id == 99 && ct.Alias == "testType")
                           && content.ContentTypeId == 10
                           && content.Level == 1
                           && content.Name == "Home"
                           && content.Path == $"-1,{parentId},{id}"
                           && content.ParentId == parentId
                           && content.SortOrder == 1
                           && content.Template == Mock.Of<ITemplate>(te => te.Id == 9 && te.Alias == "home")
                           && content.UpdateDate == DateTime.Now.AddDays(-1)
                           && content.WriterId == 1
                           && content.PropertyTypes == new List<PropertyType>(new[]
                           {
                               new PropertyType("testEditor", DataTypeDatabaseType.Nvarchar, "TestProperty1") {Name = "Test Property1", Mandatory = true, ValidationRegExp = ""},
                               new PropertyType("testEditor", DataTypeDatabaseType.Nvarchar, "testProperty2") {Name = "Test Property2", Mandatory = false, ValidationRegExp = ""}
                           })
                           && content.Properties == new PropertyCollection(new[]
                           {
                               new Property(3, Guid.NewGuid(),
                                   new PropertyType("testEditor", DataTypeDatabaseType.Nvarchar, "TestProperty1"), "property value1"),
                               new Property(3, Guid.NewGuid(),
                                   new PropertyType("testEditor", DataTypeDatabaseType.Nvarchar, "testProperty2"), "property value2"),
                           }));

            var mock = Mock.Get(c);
            mock.Setup(content => content.HasProperty(It.IsAny<string>()))
                .Returns((string alias) => alias == "TestProperty1" || alias == "testProperty2");

            return mock.Object;
        }

        public static IMedia SimpleMockedMedia(int id = 123, int parentId = 456)
        {
            var c = Mock.Of<IMedia>(
                content => content.Id == id
                           && content.CreateDate == DateTime.Now.AddDays(-2)
                           && content.CreatorId == 0
                           && content.HasIdentity == true
                           && content.ContentType == Mock.Of<IMediaType>(ct => ct.Id == 99 && ct.Alias == "testType")
                           && content.ContentTypeId == 10
                           && content.Level == 1
                           && content.Name == "Home"
                           && content.Path == "-1,123"
                           && content.ParentId == parentId
                           && content.SortOrder == 1
                           && content.UpdateDate == DateTime.Now.AddDays(-1)
                           && content.PropertyTypes == new List<PropertyType>(new[]
                           {
                               new PropertyType("testEditor", DataTypeDatabaseType.Nvarchar, "TestProperty1") {Name = "Test Property1", Mandatory = true, ValidationRegExp = ""},
                               new PropertyType("testEditor", DataTypeDatabaseType.Nvarchar, "testProperty2") {Name = "Test Property2", Mandatory = false, ValidationRegExp = ""}
                           })
                           && content.Properties == new PropertyCollection(new[]
                           {
                               new Property(3, Guid.NewGuid(),
                                   new PropertyType("testEditor", DataTypeDatabaseType.Nvarchar, "TestProperty1"), "property value1"),
                               new Property(3, Guid.NewGuid(),
                                   new PropertyType("testEditor", DataTypeDatabaseType.Nvarchar, "testProperty2"), "property value2"),
                           }));

            var mock = Mock.Get(c);
            mock.Setup(content => content.HasProperty(It.IsAny<string>()))
                .Returns((string alias) => alias == "TestProperty1" || alias == "testProperty2");

            return mock.Object;
        }

        public static IMember SimpleMockedMember(int id = 123, int parentId = 456)
        {
            var c = Mock.Of<IMember>(
                content => content.Id == id
                           && content.CreateDate == DateTime.Now.AddDays(-2)
                           && content.CreatorId == 0
                           && content.HasIdentity == true
                           && content.ContentType == Mock.Of<IMemberType>(ct => ct.Id == 99 && ct.Alias == "testType")
                           && content.ContentTypeId == 10
                           && content.Level == 1
                           && content.Name == "John Johnson"
                           && content.Path == "-1,123"
                           && content.ParentId == parentId
                           && content.SortOrder == 1
                           && content.UpdateDate == DateTime.Now.AddDays(-1)
                           && content.PropertyTypes == new List<PropertyType>(new[]
                           {
                               new PropertyType("testEditor", DataTypeDatabaseType.Nvarchar, "TestProperty1") {Name = "Test Property1", Mandatory = true, ValidationRegExp = ""},
                               new PropertyType("testEditor", DataTypeDatabaseType.Nvarchar, "testProperty2") {Name = "Test Property2", Mandatory = false, ValidationRegExp = ""}
                           })
                           && content.Properties == new PropertyCollection(new[]
                           {
                               new Property(3, Guid.NewGuid(),
                                   new PropertyType("testEditor", DataTypeDatabaseType.Nvarchar, "TestProperty1"), "property value1"),
                               new Property(3, Guid.NewGuid(),
                                   new PropertyType("testEditor", DataTypeDatabaseType.Nvarchar, "testProperty2"), "property value2"),
                           }));

            var mock = Mock.Get(c);
            mock.Setup(content => content.HasProperty(It.IsAny<string>()))
                .Returns((string alias) => alias == "TestProperty1" || alias == "testProperty2");

            return mock.Object;
        }

        


        public static IContentType SimpleMockedContentType()
        {
            var ct = Mock.Of<IContentType>();
            return ct;
        }

        public static IMediaType SimpleMockedMediaType()
        {
            var ct = Mock.Of<IMediaType>();
            return ct;
        }

        public static IMemberType SimpleMockedMemberType()
        {
            var ct = Mock.Of<IMemberType>();
            return ct;
        }

        public static IRelationType SimpleMockedRelationType()
        {
            var ct = Mock.Of<IRelationType>(
                type => type.ChildObjectType == Umbraco.Core.Constants.ObjectTypes.DocumentGuid && 
                type.ParentObjectType == Umbraco.Core.Constants.ObjectTypes.DocumentGuid &&
                type.Alias == "testType");
            return ct;
        }

        public static IRelation SimpleMockedRelation(int id, int child, int parent, IRelationType relType)
        {
            var r = Mock.Of<IRelation>(content =>
                    content.ChildId == child &&
                    content.ParentId == parent &&
                    content.Id == id &&
                    content.RelationType == relType && 
                    content.CreateDate == DateTime.Now);

            return r;
        }

        public static IPublishedContent SimpleMockedPublishedContent(int id = 123, int? parentId = null, int? childId = null)
        {
            return Mock.Of<IPublishedContent>(
                content => content.Id == id
                           && content.IsDraft == false
                           && content.CreateDate == DateTime.Now.AddDays(-2)
                           && content.CreatorId == 0
                           && content.CreatorName == "admin"
                           && content.DocumentTypeAlias == "test"
                           && content.DocumentTypeId == 10
                           && content.ItemType == PublishedItemType.Content
                           && content.Level == 1
                           && content.Name == "Home"
                           && content.Path == "-1,123"
                           && content.SortOrder == 1
                           && content.TemplateId == 9
                           && content.UpdateDate == DateTime.Now.AddDays(-1)
                           && content.Url == "/home"
                           && content.UrlName == "home"
                           && content.WriterId == 1
                           && content.WriterName == "Editor"
                           && content.Properties == new List<IPublishedProperty>(new[]
                           {
                               Mock.Of<IPublishedProperty>(property => property.HasValue == true
                                                                       && property.PropertyTypeAlias == "TestProperty1"
                                                                       && property.DataValue == "raw value"
                                                                       && property.Value == "Property Value"),
                               Mock.Of<IPublishedProperty>(property => property.HasValue == true
                                                                       && property.PropertyTypeAlias == "testProperty2"
                                                                       && property.DataValue == "raw value"
                                                                       && property.Value == "Property Value")
                           })
                           && content.Parent == (parentId.HasValue ? SimpleMockedPublishedContent(parentId.Value, null, null) : null)
                           && content.Children == (childId.HasValue ? new[] { SimpleMockedPublishedContent(childId.Value, null, null) } : Enumerable.Empty<IPublishedContent>()));
        }
    }
}
