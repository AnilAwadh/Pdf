# UglyToad.Pdf #

[![Build status](https://ci.appveyor.com/api/projects/status/ni7et2j2ml60pdi3?svg=true)](https://ci.appveyor.com/project/EliotJones/pdf)
[![codecov](https://codecov.io/gh/UglyToad/Pdf/branch/master/graph/badge.svg)](https://codecov.io/gh/UglyToad/Pdf)

The aim of this project is to convert the [PdfBox](https://github.com/apache/pdfbox) code to C# in order to provide a properly open source (i.e. no copyleft) solution for inspecting PDF documents. This uses the Apache 2.0 licence.

## Status ##

There is a lot left to do for this project, the initial minimum viable product when released to Alpha will provide:

+ Page counts and sizes (in points) for a document.
+ Access to the text contents of each page. Note that since PDF has no concept of a "word" it will be up to the consumer of the text to work out where the words are within the text.
+ (Possible) The locations of each letter on the page.

For the initial alpha release all files will be opened rather than streamed so this will not support large files.

Eventually the library should support all existing PdfBox operations such as accessing graphical elements, form elements as well as creating PDF documents.

## Usage ##

The initial public API will be as limited as possible to allow extensive refactoring to take place. The proposed usage is as follows:

    using (PdfDocument document = PdfDocument.Open(@"C:\my-file.pdf"))
    {
        int pageCount = document.NumberOfPages;

        Page page = document.GetPage(1);

        decimal widthInPoints = page.Width;
        decimal heightInPoints = page.Height;

        string text = page.Text;
    }
    
```PdfDocument``` should only be used in a ```using``` statement since it is disposable (unless the consumer disposes of it elsewhere).
    
The ```Page``` contains the page width and height in points as well as mapping to the ```PageSize``` enum:

    PageSize size = Page.Size;
    
    bool isA4 = size == PageSize.A4;

The ```PdfDocument``` will also support opening from byte arrays (as well as streams eventually):

    byte[] fileBytes = File.ReadAllBytes(@"C:\my-file.pdf");
    (using PdfDocument document = PdfDocument.Open(fileBytes))
    {
        int numberOfPages = document.NumberOfPages;
    }

The ```PdfDocument``` provides access to the document metadata defined in the PDF file, most of these entries will be null:

    PdfDocument document = PdfDocument.Open(fileName);
    // The name of the program used to convert this document to PDF.
    string producer = document.DocumentInformation.Producer;
    // The title given to the document
    string title = document.DocumentInformation.Title;
    // etc...
