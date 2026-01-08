using SharedClientSide.ServerInteraction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SharedClientSide;
using DownloadHDAvalonia.Services;
using SkiaSharp;

namespace DownloadHDAvalonia.Services
{
    public class DownloadHDPhotosInBatchService
    {
        public struct ProgressCallbackContent
        {
            public int currentIndex;
            public int total;
            public string message;
        }
        public struct DownloadAllPhotosResult
        {
            public bool success;
            public List<string> missingPhotos;
        }

        public List<PhotoData> listPhotoData { get; set; } = new List<PhotoData>();

        public async Task<List<PhotoBlobReference>> GetPhotoBlobReferences(string classCode, string pathFilter)
        {
            var photos = await LesserFunctionClient.DefaultClient.GetImagesConfigAsStringLists(classCode);

            var evphoto = photos.eventsImagesConfig.Select(x => new PhotoBlobReference() { ImageConfig = x, Foldertype = PhotoBlobReference.EventFolderTypes.EVENTOS }).ToList();

            var allPhotos = photos.eventsImagesConfig.Select(x => new PhotoBlobReference() { ImageConfig = x, Foldertype = PhotoBlobReference.EventFolderTypes.EVENTOS }).ToList();
            allPhotos.AddRange(photos.recImagesConfig.Select(x => new PhotoBlobReference() { ImageConfig = x, Foldertype = PhotoBlobReference.EventFolderTypes.RECONHECIMENTOS }));
            if(pathFilter != null)
                allPhotos.RemoveAll(x => x.ShortPath.Contains(pathFilter) == false);

            return allPhotos;
        }
        public async Task<DownloadAllPhotosResult> DownloadAllPhotosInClass(ProfessionalTask pt, DirectoryInfo outputFolder, string pathFilter, Action<ProgressCallbackContent> progressCallback, bool preferAutoTreated = false, bool smartCompress = true)
        {
            if (pt is null)
            {
                throw new ArgumentNullException(nameof(pt));
            }

            if (outputFolder is null)
            {
                throw new ArgumentNullException(nameof(outputFolder));
            }

            if (progressCallback is null)
            {
                throw new ArgumentNullException(nameof(progressCallback));
            }

            if (pt.AutoTreatment == true && pt.AutoTreatmentVersion == "2.0" && preferAutoTreated == true)
            {
                int count = 0;
                while (listPhotoData.Count == 0)
                {
                    Console.WriteLine("search photoData");
                    await Task.Delay(1500);
                    await GetAllCollectionPhotosByClassCode(pt);
                    count++;

                    if (count > 20)
                    {
                        Console.WriteLine("Não foi possível obter os dados de fotos do servidor");
                        break;
                    }
                }
            }

            var photoBlobReferences = await GetPhotoBlobReferences(pt.classCode, pathFilter);
            var tasks = new List<Task>();
            for (int i = 0; i < photoBlobReferences.Count; i++)
            {
                try
                {
                    PhotoBlobReference photo = photoBlobReferences[i];
                    string localOutputFilePath = $"{outputFolder}/{photo.Foldertype.ToString()}{photo.ShortPath}";

                    if (File.Exists(localOutputFilePath))
                        continue;
                    while (tasks.Count > 10)
                    {
                        await Task.Delay(10);
                        tasks.RemoveAll(x => x.IsCompleted);
                    }
                    Directory.CreateDirectory(@"\\?\" + new FileInfo(localOutputFilePath).Directory.FullName);
                    string localOutputFilePathLongPath = localOutputFilePath;

                    var taskCount = tasks.Count;
                    if (pt.AutoTreatment == true)
                    {

                        bool AutoTreatmentIsVersion2_0 = pt.AutoTreatmentVersion == "2.0";

                        tasks.Add(DownloadPhoto(pt.classCode, photo.Foldertype.ToString() + photo.ShortPath, pt.StorageLocation, localOutputFilePathLongPath, preferAutoTreated, AutoTreatmentIsVersion2_0, smartCompress));
                        taskCount = tasks.Count / 2; // when downloading 2 files per photo, we need to divide the task count by 2

                    }
                    else
                    {
                        tasks.Add(DownloadPhoto(pt.classCode, photo.Foldertype.ToString() + photo.ShortPath, pt.StorageLocation, localOutputFilePathLongPath, false, false, smartCompress));
                    }
                    progressCallback(new ProgressCallbackContent() { currentIndex = i + 1 - taskCount, total = photoBlobReferences.Count, message = "Baixando fotos" });


                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }

            while (tasks.Count > 0)
            {
                await Task.Delay(10);
                tasks.RemoveAll(x => x.IsCompleted);

                progressCallback(new ProgressCallbackContent() { currentIndex = photoBlobReferences.Count - tasks.Count, total = photoBlobReferences.Count, message = "Baixando as últimas fotos" });
            }

            progressCallback(new ProgressCallbackContent() { currentIndex = photoBlobReferences.Count - tasks.Count, total = photoBlobReferences.Count, message = "Verificando integridade download" });

            var r = VerifyDownloadIntegrity(photoBlobReferences, outputFolder, smartCompress);

            progressCallback(new ProgressCallbackContent() { currentIndex = photoBlobReferences.Count - tasks.Count, total = photoBlobReferences.Count, message = "Concluído" });

            await Task.WhenAll(tasks);
            return r;
        }

        public async Task<string> GetAllCollectionPhotosByClassCode(ProfessionalTask pt)
        {
            var r = await LesserFunctionClient.DefaultClient.GetAllCollectionPhotosByClassCode(pt.classCode);
            if (r != null && r.Content != null)
            {
                for (int i = 0; i < r.Content.Count; i++)
                {
                    listPhotoData.Add(r.Content[i]);
                }
            }
            return "";
        }

        private DownloadAllPhotosResult VerifyDownloadIntegrity(List<PhotoBlobReference> photoBlobReferences, DirectoryInfo outputFolder, bool smartCompress)
        {
            var missingPhotos = new List<string>();
            foreach (var photoConfig in photoBlobReferences)
            {
                string photoPath = $"{outputFolder}/{photoConfig.Foldertype.ToString()}{photoConfig.ShortPath}";
                if (smartCompress == false)
                {
                    photoPath = Path.ChangeExtension(photoPath, ".png");
                }
                var fileInfo = new FileInfo(photoPath);
                if(!fileInfo.Exists)
                {
                    missingPhotos.Add(photoPath);
                }
                else if (fileInfo.Length < 10 * 1024) // less than 10KB
                {
                    missingPhotos.Add(photoConfig.ShortPath);
                }
            }

            if (missingPhotos.Count == 0)
            {
                return new DownloadAllPhotosResult() { missingPhotos = missingPhotos, success = true };
            }
            else
            {
                return new DownloadAllPhotosResult() { missingPhotos = missingPhotos, success = false };
            }
        }

        private async Task DownloadPhoto(string classCode, string shortPath, string storageLocation, string outputPath, bool preferAutoTreated, bool AutoTreatmentVersionIs2_0 = false, bool smartCompress = true)
        {
            var ur = await LesserFunctionClient.DefaultClient.GetPhotoBytesFromClassesOrHDPhotosContainer(classCode, shortPath, true, storageLocation, preferAutoTreated);

            string shortPathAdjusted = shortPath.Replace("1.Eventos/", "").Replace("2.Reconhecimentos/","");
            var data = listPhotoData.FirstOrDefault(x => x.ShortPath == shortPathAdjusted);


            var correctionData = "";

            if (smartCompress == true) 
            {

                if (AutoTreatmentVersionIs2_0)
                {
                    if(data != null && data.ColorCorrectionData != null && data.ColorCorrectionData != "")
                    {
                        correctionData = data.ColorCorrectionData;
                        var colorCorrectionData = JsonConvert.DeserializeObject<TreatmentAdjusts>(correctionData);

                        var img = ColorCorrection.ImageProcessor.ProcessImage(ur, colorCorrectionData);
                        if (img != null)
                        {
                            File.WriteAllBytes(outputPath, img);
                        }
                    }
                    else
                    {
                        File.WriteAllBytes(outputPath, ur);
                    }
                }
                else
                {
                    File.WriteAllBytes(outputPath, ur);
                }
            } else
            {
                var pngOutputPath = Path.ChangeExtension(outputPath, ".png");
                using (var input = new MemoryStream(ur))
                using (var skBitmap = SKBitmap.Decode(input))
                {
                    if (skBitmap != null)
                    {
                        using (var image = SKImage.FromBitmap(skBitmap))
                        using (var pngData = image.Encode(SKEncodedImageFormat.Png, 100))
                        {
                            using (var outputStream = new FileStream(pngOutputPath, FileMode.Create))
                            {
                                pngData.SaveTo(outputStream);
                            }
                        }
                    }
                }
            }
            GC.Collect();
        }
        public class PhotoBlobReference
        {

            public string ImageConfig;
            public string ShortPath
            {
                get
                {
                    return ImageConfig.Split(';')[0].Replace("\\", "/");
                }
            }
            public EventFolderTypes Foldertype;
            public class EventFolderTypes
            {
                internal static EventFolderTypes EVENTOS { get { return new EventFolderTypes("1.Eventos"); } }
                internal static EventFolderTypes RECONHECIMENTOS { get { return new EventFolderTypes("2.Reconhecimentos"); } }
                private string type;
                private EventFolderTypes(string type)
                {
                    this.type = type;
                }
                public override string ToString()
                {
                    return type;
                }
            }
        }
    }
}




