Imports System.Data
Imports System.Data.SqlClient
Imports System.Text
Imports System.Threading.Tasks
Imports System.Web.Script.Serialization

Public Class ProductCatalog
    Inherits System.Web.UI.Page

    Private Const SqlConnectionString As String = "Server=localhost;Database=DataExposed;Trusted_Connection=True;"
    Private Const OllamaEndpoint As String = "http://localhost:11434/api/embeddings"
    Private Const OllamaModel As String = "nomic-embed-text"

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        ' Page initializations go here
    End Sub

    Protected Sub btnSave_Click(sender As Object, e As EventArgs)
        If String.IsNullOrEmpty(txtProdName.Text) OrElse String.IsNullOrEmpty(txtProdDesc.Text) Then
            lblStatus.Text = "Please fill in all input boxes."
            Return
        End If

        Page.RegisterAsyncTask(New PageAsyncTask(Async Function()
                                                     Try
                                                         lblStatus.ForeColor = System.Drawing.Color.Blue
                                                         lblStatus.Text = "Contacting Ollama service..."

                                                         Dim rawOllamaJson As String = Await GetOllamaEmbeddingAsync(txtProdDesc.Text)

                                                         If String.IsNullOrEmpty(rawOllamaJson) Then
                                                             lblStatus.ForeColor = System.Drawing.Color.Red
                                                             lblStatus.Text = "Ollama connection failed or returned no data."
                                                             Return
                                                         End If

                                                         lblStatus.Text = "Writing vector to SQL Server 2025..."
                                                         Using conn As New System.Data.SqlClient.SqlConnection(SqlConnectionString)
                                                             ' FIX: Cast to VARCHAR(MAX) inside STRING_AGG to allow long vector strings
                                                             Dim query As String =
                        "DECLARE @NestedJSON NVARCHAR(MAX); " &
                        "SELECT @NestedJSON = CASE " &
                        "    WHEN JSON_QUERY(@RawJson, '$.embeddings') IS NOT NULL THEN JSON_QUERY(@RawJson, '$.embeddings') " &
                        "    ELSE JSON_QUERY(@RawJson, '$.embedding') END; " &
                        "DECLARE @CleanVectorStr NVARCHAR(MAX); " &
                        "SELECT @CleanVectorStr = '[' + STRING_AGG(CAST([value] AS VARCHAR(MAX)), ',') + ']' " &
                        "FROM OPENJSON(@NestedJSON); " &
                        "INSERT INTO Products (ProductName, ProductDescription, DescriptionVector) " &
                        "VALUES (@Name, @Desc, CAST(@CleanVectorStr AS VECTOR(768)));"

                                                             Using cmd As New System.Data.SqlClient.SqlCommand(query, conn)
                                                                 cmd.Parameters.AddWithValue("@Name", txtProdName.Text)
                                                                 cmd.Parameters.AddWithValue("@Desc", txtProdDesc.Text)
                                                                 cmd.Parameters.AddWithValue("@RawJson", rawOllamaJson)

                                                                 Await conn.OpenAsync()
                                                                 Await cmd.ExecuteNonQueryAsync()
                                                             End Using
                                                         End Using

                                                         lblStatus.ForeColor = System.Drawing.Color.Green
                                                         lblStatus.Text = "Saved successfully! Vector computed and saved locally via Ollama."
                                                         txtProdName.Text = ""
                                                         txtProdDesc.Text = ""

                                                     Catch ex As System.Data.SqlClient.SqlException
                                                         lblStatus.ForeColor = System.Drawing.Color.Red
                                                         lblStatus.Text = "SQL Server Error: " & ex.Message & " (Line " & ex.LineNumber & ")"
                                                     Catch ex As System.Net.WebException
                                                         lblStatus.ForeColor = System.Drawing.Color.Red
                                                         lblStatus.Text = "Ollama Network Error: Is Ollama running? Details: " & ex.Message
                                                     Catch ex As Exception
                                                         lblStatus.ForeColor = System.Drawing.Color.Red
                                                         lblStatus.Text = "General Error: " & ex.Message
                                                     End Try
                                                 End Function))
    End Sub

    Protected Sub btnSearch_Click(sender As Object, e As EventArgs) Handles btnSearch.Click
        If String.IsNullOrEmpty(txtSearch.Text) Then Return

        Page.RegisterAsyncTask(New PageAsyncTask(Async Function()
                                                     Try
                                                         Dim rawOllamaJson As String = Await GetOllamaEmbeddingAsync(txtSearch.Text)

                                                         If String.IsNullOrEmpty(rawOllamaJson) Then
                                                             lblStatus.ForeColor = System.Drawing.Color.Red
                                                             lblStatus.Text = "Could not generate vector from user query."
                                                             Return
                                                         End If

                                                         Using conn As New System.Data.SqlClient.SqlConnection(SqlConnectionString)
                                                             ' FIX: Cast to VARCHAR(MAX) inside STRING_AGG here as well
                                                             Dim query As String =
                        "DECLARE @NestedJSON NVARCHAR(MAX); " &
                        "SELECT @NestedJSON = CASE " &
                        "    WHEN JSON_QUERY(@RawJson, '$.embeddings') IS NOT NULL THEN JSON_QUERY(@RawJson, '$.embeddings') " &
                        "    ELSE JSON_QUERY(@RawJson, '$.embedding') END; " &
                        "DECLARE @CleanVectorStr NVARCHAR(MAX); " &
                        "SELECT @CleanVectorStr = '[' + STRING_AGG(CAST([value] AS VARCHAR(MAX)), ',') + ']' " &
                        "FROM OPENJSON(@NestedJSON); " &
                        "DECLARE @SearchVector VECTOR(768) = CAST(@CleanVectorStr AS VECTOR(768)); " &
                        "SELECT TOP 3 ProductName, ProductDescription, " &
                        "VECTOR_DISTANCE('cosine', DescriptionVector, @SearchVector) AS Distance " &
                        "FROM Products ORDER BY Distance ASC;"

                                                             Using cmd As New System.Data.SqlClient.SqlCommand(query, conn)
                                                                 cmd.Parameters.AddWithValue("@RawJson", rawOllamaJson)

                                                                 Using sda As New System.Data.SqlClient.SqlDataAdapter(cmd)
                                                                     Dim dt As New System.Data.DataTable()
                                                                     Await conn.OpenAsync()
                                                                     sda.Fill(dt)

                                                                     gvResults.DataSource = dt
                                                                     gvResults.DataBind()
                                                                 End Using
                                                             End Using
                                                         End Using
                                                     Catch ex As Exception
                                                         lblStatus.ForeColor = System.Drawing.Color.Red
                                                         lblStatus.Text = "Search Error: " & ex.Message
                                                     End Try
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