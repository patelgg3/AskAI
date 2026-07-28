Imports System.Data
Imports System.Data.SqlClient
Imports System.Text
Imports System.Threading.Tasks
Partial Class VectorUpdate
    Inherits System.Web.UI.Page

    Private Const SqlConnectionString As String = "Server=localhost;Database=DataExposed;Trusted_Connection=True;"
    Private Const OllamaEndpoint As String = "http://localhost:11434/api/embeddings"
    Private Const OllamaModel As String = "nomic-embed-text"
    Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
        'BulkUpdate()
    End Sub

    Protected Sub btnBulkUpdate_Click(sender As Object, e As EventArgs) Handles btnBulkUpdate.Click
        BulkUpdate()
    End Sub
    Private Sub BulkUpdate()
        Page.RegisterAsyncTask(New PageAsyncTask(Async Function()
                                                     Dim missingItemsTable As New DataTable()
                                                     Dim updateCount As Integer = 0
                                                     Dim failureCount As Integer = 0

                                                     Try
                                                         lblStatus.ForeColor = System.Drawing.Color.Blue
                                                         lblStatus.Text = "Scanning database for un-vectorized products..."

                                                         ' 1. Fetch all rows where DescriptionVector is currently NULL
                                                         Using conn As New SqlConnection(SqlConnectionString)
                                                             Dim selectQuery As String = "SELECT ProductID, ProductDescription FROM Products WHERE DescriptionVector IS NULL;"
                                                             Using cmd As New SqlCommand(selectQuery, conn)
                                                                 Using sda As New SqlDataAdapter(cmd)
                                                                     Await conn.OpenAsync()
                                                                     sda.Fill(missingItemsTable)
                                                                 End Using
                                                             End Using
                                                         End Using

                                                         ' Line 38 Correction path
                                                         If missingItemsTable.Rows.Count = 0 Then
                                                             lblStatus.ForeColor = System.Drawing.Color.Green
                                                             lblStatus.Text = "All existing products already have valid vector embeddings!"
                                                             Return Nothing
                                                         End If

                                                         ' 2. Loop through every row and generate its embedding vector using Ollama
                                                         For Each row As DataRow In missingItemsTable.Rows
                                                             Dim currentId As Integer = Convert.ToInt32(row("ProductID"))
                                                             Dim currentDescription As String = row("ProductDescription").ToString()

                                                             ' Request the raw JSON string payload block straight from Ollama
                                                             Dim rawOllamaJson As String = Await GetOllamaEmbeddingAsync(currentDescription)

                                                             If Not String.IsNullOrEmpty(rawOllamaJson) Then
                                                                 ' 3. Update this specific row inside SQL Server using our OPENJSON array unpacker
                                                                 Using conn As New SqlConnection(SqlConnectionString)
                                                                     Dim updateQuery As String =
                                "DECLARE @NestedJSON NVARCHAR(MAX); " &
                                "SELECT @NestedJSON = CASE " &
                                "    WHEN JSON_QUERY(@RawJson, '$.embeddings') IS NOT NULL THEN JSON_QUERY(@RawJson, '$.embeddings') " &
                                "    ELSE JSON_QUERY(@RawJson, '$.embedding') END; " &
                                "DECLARE @CleanVectorStr NVARCHAR(MAX); " &
                                "SELECT @CleanVectorStr = '[' + STRING_AGG(CAST([value] AS VARCHAR(MAX)), ',') + ']' " &
                                "FROM OPENJSON(@NestedJSON); " &
                                "UPDATE Products SET DescriptionVector = CAST(@CleanVectorStr AS VECTOR(768)) " &
                                "WHERE ProductID = @ID;"

                                                                     Using cmd As New SqlCommand(updateQuery, conn)
                                                                         cmd.Parameters.AddWithValue("@RawJson", rawOllamaJson)
                                                                         cmd.Parameters.AddWithValue("@ID", currentId)

                                                                         Await conn.OpenAsync()
                                                                         Dim affected As Integer = Await cmd.ExecuteNonQueryAsync()
                                                                         If affected > 0 Then updateCount += 1
                                                                     End Using
                                                                 End Using
                                                             Else
                                                                 failureCount += 1
                                                             End If
                                                         Next

                                                         ' 4. Provide a final summary statement to the user
                                                         lblStatus.ForeColor = System.Drawing.Color.Green
                                                         lblStatus.Text = String.Format("Batch completed! Successfully updated {0} items. (Failed rows: {1})", updateCount, failureCount)

                                                     Catch ex As SqlException
                                                         lblStatus.ForeColor = System.Drawing.Color.Red
                                                         lblStatus.Text = "Database Batch Error: " & ex.Message
                                                     Catch ex As Exception
                                                         lblStatus.ForeColor = System.Drawing.Color.Red
                                                         lblStatus.Text = "General Batch Error: " & ex.Message
                                                     End Try

                                                     Return Nothing
                                                 End Function))
    End Sub
    Private Async Function GetOllamaEmbeddingAsync(textToEmbed As String) As Task(Of String)
        Try
            Using client As New System.Net.WebClient()
                client.Headers(System.Net.HttpRequestHeader.ContentType) = "application/json"
                client.Encoding = Encoding.UTF8

                Dim cleanText As String = textToEmbed.Replace("""", "\""").Replace(vbCrLf, " ").Replace(vbLf, " ")
                Dim jsonPayload As String = "{""model"":""" & OllamaModel & """,""prompt"":""" & cleanText & """}"

                ' Simply return the raw JSON string straight from the source
                Dim jsonResponse As String = Await Task.Run(Function()
                                                                Return client.UploadString(OllamaEndpoint, "POST", jsonPayload)
                                                            End Function)
                Return jsonResponse
            End Using
        Catch ex As Exception
            System.Diagnostics.Debug.WriteLine("Ollama Core Capture Exception: " & ex.Message)
            Return Nothing
        End Try
    End Function
End Class
