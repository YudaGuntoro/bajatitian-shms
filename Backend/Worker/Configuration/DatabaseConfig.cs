namespace Worker.Configuration;

public static class DatabaseConfig
{
	private const string DefaultConnectionString = "Server=127.0.0.1;Port=3306;User ID=root;Password=root_native;Database=bajatitian_shms;SslMode=None;AllowPublicKeyRetrieval=True;";

	public static string MysqlConnString => Config.Instance.Read("ConnectionString", "Database") ?? DefaultConnectionString;
}
