﻿using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HttpRecorder.Repositories.HAR
{
    /// <summary>
    /// <see cref="IInteractionRepository"/> implementation that stores <see cref="Interaction"/>
    /// in files in the HTTP Archive format (https://en.wikipedia.org/wiki/.har / https://w3c.github.io/web-performance/specs/HAR/Overview.html).
    /// </summary>
    /// <remarks>
    /// The interactionName parameter is used as the file path.
    /// The .har extension will be added if no file extension is provided.
    /// </remarks>
    public class HttpArchiveInteractionRepository : IInteractionRepository
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
#pragma warning disable SYSLIB0020
            IgnoreNullValues = true,
#pragma warning restore SYSLIB0020
            WriteIndented = true,
        };

        /// <inheritdoc />
        public Task<bool> ExistsAsync(string interactionName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(File.Exists(GetFilePath(interactionName)));
        }

        /// <inheritdoc />
        public Task<Interaction> LoadAsync(string interactionName, CancellationToken cancellationToken = default)
        {
            try
            {
                var archive = JsonSerializer.Deserialize<HttpArchive>(
                File.ReadAllText(GetFilePath(interactionName), Encoding.UTF8),
                _jsonOptions);

                return Task.FromResult(archive.ToInteraction(interactionName));
            }
            catch (Exception ex) when ((ex is IOException) || (ex is JsonException))
            {
                throw new HttpRecorderException($"Error while loading file {GetFilePath(interactionName)}: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<Interaction> StoreAsync(Interaction interaction, CancellationToken cancellationToken = default)
        {
            if (interaction == null)
            {
                throw new ArgumentNullException(nameof(interaction));
            }

            var filePath = GetFilePath(interaction.Name);
            try
            {
                var archive = new HttpArchive(interaction);
                var archiveDirectory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(archiveDirectory) && !Directory.Exists(archiveDirectory))
                {
                    Directory.CreateDirectory(archiveDirectory);
                }

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await JsonSerializer.SerializeAsync(stream, archive, _jsonOptions, cancellationToken);
                }

                return archive.ToInteraction(interaction.Name);
            }
            catch (Exception ex) when ((ex is IOException) || (ex is JsonException))
            {
                throw new HttpRecorderException($"Error while writing file {filePath}: {ex.Message}", ex);
            }
        }

        private string GetFilePath(string interactionName)
            => Path.HasExtension(interactionName)
                ? Path.GetFullPath(interactionName)
                : Path.GetFullPath($"{interactionName}.har");
    }
}
