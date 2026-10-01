using Microsoft.Data.SqlClient;

namespace SmartLabel_Vision
{
    public static class DatabaseLogger
    {
        private const string ConnectionString =
            @"Server=localhost\SQLEXPRESS;Database=SmartLabelDB;Trusted_Connection=True;TrustServerCertificate=True;";

        public static void Log(InspectionResult res)
        {
            const string sql = @"
                INSERT INTO InspectionLogs (BarcodeData, ExpiryDateText, ContourAreaRatio, Verdict, FailureReason, CycleTimeMs)
                VALUES (@Barcode, @Expiry, @Ratio, @Verdict, @Reason, @CycleTime);";

            using var conn = new SqlConnection(ConnectionString);
            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@Barcode", (object?)res.BarcodeData ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Expiry", (object?)res.ExpiryText ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ratio", res.AreaRatio);
            cmd.Parameters.AddWithValue("@Verdict", res.Verdict);
            cmd.Parameters.AddWithValue("@Reason", string.IsNullOrEmpty(res.FailureReason) ? (object)DBNull.Value : res.FailureReason);
            cmd.Parameters.AddWithValue("@CycleTime", res.CycleTimeMs);

            conn.Open();
            cmd.ExecuteNonQuery();
        }
    }
}