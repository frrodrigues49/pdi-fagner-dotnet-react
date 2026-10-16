using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace Pdi.Onboarding.Api.Infrastructure.Mongo;

public static class MongoConventions
{
  private static bool _registered;

  public static void Register()
  {
    // O BsonSerializer é global (estático): registrar duas vezes lança exceção.
    if (_registered)
      return;
    _registered = true;

    BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
    BsonSerializer.RegisterSerializer(new DateOnlySerializer());

    ConventionRegistry.Register(
        "pdi",
        new ConventionPack
        {
          new CamelCaseElementNameConvention(),
          new EnumRepresentationConvention(BsonType.String),
          new IgnoreExtraElementsConvention(true)
        },
        _ => true);
  }
}