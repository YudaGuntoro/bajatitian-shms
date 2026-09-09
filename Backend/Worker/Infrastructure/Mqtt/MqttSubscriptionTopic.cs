namespace Worker.Infrastructure.Mqtt;

public sealed record MqttSubscriptionTopic(string Topic, int Qos);
