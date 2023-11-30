//using Newtonsoft.Json;
//using System.Xml.Linq;
//using Umbraco.Cms.Core.Composing;
//using Umbraco.Cms.Core.Services;

//namespace Mikkelhm.Core.Components
//{
//    public class ContentImporter : IComponent
//    {
//        private readonly IContentService _contentService;

//        public ContentImporter(IContentService contentService)
//        {
//            _contentService = contentService;
//        }

//        public void Initialize()
//        {
//            var path = @"d:\temp\package.xml";

//            XDocument xDoc = XDocument.Load(path);

//            var nodes = xDoc.Elements("umbPackage").Elements("Documents").Elements("DocumentSet").Elements("Articulate").Elements("ArticulateArchive").Elements();
//            var posts = nodes.Select(x => new ArticulatePost()
//            {
//                Name = x.Attribute("nodeName").Value,
//                UrlName = x.Attribute("urlName").Value,
//                Excerpt = x.Element("excerpt").Value,
//                Categories = x.Element("categories")?.Value,
//                Tags = x.Element("tags")?.Value,
//                PublishedDate = DateTime.Parse(x.Element("publishedDate").Value),
//                UpdateDate = DateTime.Parse(x.Attribute("updateDate").Value),
//                CreateDate = DateTime.Parse(x.Attribute("createDate").Value),
//                Body = x.Element("richText") == null ? x.Element("markdown").Value : x.Element("richText").Value,
//                References = x.Element("references")?.Value
//            });

//            var currentChildren = _contentService.GetPagedChildren(1100, 0, 1000, out var children);
//            foreach (var post in posts)
//            {
//                var existing = currentChildren.FirstOrDefault(x => x.Name.Equals(post.Name, StringComparison.InvariantCultureIgnoreCase));
//                if (existing != null)
//                {
//                    existing.SetValue("excerpt", post.Excerpt);
//                    var newTags = new List<string>();
//                    var categories = post.Categories == null ? new string[0] : post.Categories.Split(',');
//                    var tags = post.Tags == null ? new string[0] : post.Tags.Split(",");
//                    newTags.AddRange(categories);
//                    newTags.AddRange(tags);
//                    existing.SetValue("tags", JsonConvert.SerializeObject(newTags.Distinct().ToArray()));
//                    existing.SetValue("publishedDate", post.PublishedDate);
//                    existing.UpdateDate = post.UpdateDate;
//                    existing.CreateDate = post.CreateDate;
//                    existing.SetValue("body", post.Body);
//                    existing.SetValue("references", post.References);
//                    _contentService.SaveAndPublish(existing);
//                }
//                else
//                {
//                    var content = _contentService.Create(post.Name, 1100, "blogPost");
//                    content.SetValue("excerpt", post.Excerpt);
//                    var newTags = new List<string>();
//                    var categories = post.Categories == null ? new string[0] : post.Categories.Split(',');
//                    var tags = post.Tags == null ? new string[0] : post.Tags.Split(",");
//                    newTags.AddRange(categories);
//                    newTags.AddRange(tags);
//                    content.SetValue("tags", JsonConvert.SerializeObject(newTags.ToArray()));
//                    content.SetValue("publishedDate", post.PublishedDate);
//                    content.UpdateDate = post.UpdateDate;
//                    content.CreateDate = post.CreateDate;
//                    content.SetValue("body", post.Body);
//                    content.SetValue("references", post.References);
//                    _contentService.SaveAndPublish(content);
//                }
//            }
//        }

//        public void Terminate()
//        {
//        }

//        public class ArticulatePost
//        {
//            public string Name { get; set; }
//            public string UrlName { get; set; }
//            public string Excerpt { get; set; }
//            public string Categories { get; set; }
//            public string Tags { get; set; }
//            public DateTime PublishedDate { get; set; }
//            public DateTime UpdateDate { get; set; }
//            public DateTime CreateDate { get; set; }
//            public string Body { get; set; }
//            public string References { get; set; }
//        }
//    }


//}
