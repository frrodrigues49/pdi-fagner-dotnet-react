using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace Pdi.Onboarding.Worker.Mongo;

public static class MongoConventions
{
  private static bool _registered;

  public static void Register()
  {
    // O BsonSerializer é global (estático) para o PROCESSO inteiro: se outro componente
    // (ex.: as duas APIs no mesmo teste de integração) já registrou, Try* não lança.
    if (_registered)
      return;
    _registered = true;

    BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
    BsonSerializer.TryRegisterSerializer(new DateOnlySerializer());

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