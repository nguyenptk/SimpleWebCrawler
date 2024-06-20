# Simple Web Crawler

## Overview

Simple Web Crawler is a basic tool designed to crawl data from news websites. Currently, it supports `https://vnexpress.net` & `https://tuoitre.vn` to fetch the top 10 articles by total likes from the past week. Each successfully fetched article is stored in the `out/raw.json` file, then tracking the top 10 articles aggregated by likes. For example, see [this sample article](https://vnexpress.net/canh-sat-meo-vac-lao-xuong-suoi-cuu-nguoi-4756681.html#box_comment_vne).

## System Design

### Objectives

- Design a backend service to crawl articles and calculate their total likes.
- Design a simple UI to execute the crawler and load the top 10 liked articles.
- Ensure idempotent requests, allowing only one request per website at a time.

### Architecture

```plaintext
Client (Browser)
       |
       v
Backend (Controller)
       |
       +--> Idempotence Check
       |         |
       |         v
       |   HandlerFactory
       |         |
       |         +--> VNExpressHandler (https://vnexpress.net/)
       |         |         |
       |         |         +--> Fetch & Process Articles
       |         |         |
       |         |         +--> Write to Storage [out/raw.json]
       |         |         |
       |         |         +--> Write to Storage [out/top_articles_vnexpress.json]
       |         |
       |         +--> TuoiTreHandler (https://tuoitre.net/)
       |                   |
       |                   +--> Fetch & Process Articles
       |                   |
       |                   +--> Write to Storage [out/raw.json]
       |                   |
       |                   +--> Write to Storage [out/top_articles_tuoitre.json]
       |
       +--> LoadHandler
                 |
                 +--> Aggregate Top Articles
                 |
```

### Components

The project consists of a client and backend, networked together using Docker Compose:

- **Backend**: Written in C# to support multithreading, utilizing `HtmlAgilityPack` for HTML content fetching and `PuppeteerSharp` for asynchronous AJAX fetching.
- **Client**: A simple HTML file using JavaScript to interact with `execute` and `load` APIs, served by an Nginx server.
- **Storage**: Handled via JSON files.

The core of the system is a server written in C#. Upon receiving a client request, the server fetches the main URL to retrieve primary articles, then navigates through menu and sub-menu links to gather additional articles.

> The deployment environment can be configured with two options: `BATCH_MENU` and `BATCH_ARTICLE`. These control the number of threads for crawling articles and should be set to less than the total number of CPUs available on the machine. Depending on the current processing handlers, `BATCH_MENU` and `BATCH_ARTICLE` will be distributed to the handlers fairly.

#### Workflow

1. **Initialization**:
   - `Controller` handles routing and ensures idempotent requests.
   - `CrawlerHandlerFactory` encapsulate the creation logic of different crawler handlers
   - `ICrawlerHandler` sets the execution start time and configures batch sizes.
   - `HttpClient` fetches the main HTML content from the homepage URL.

2. **Menu Link Extraction**:
   - HTML content is parsed to extract menu links using XPath.
   - Links are normalized to absolute URLs.

3. **Menu Link Processing**:
   - A headless browser instance is launched using PuppeteerSharp for each batch of menu links.
   - HTML content and sub-menu links are fetched and processed in parallel.

4. **Sub-Menu Link and Article Processing**:
   - Sub-menu links are processed in batches, and articles are extracted from the fetched HTML content.

5. **Article Details Extraction**:
   - For each article, details such as website, title, URL, publication date, and total likes are extracted.
   - Only articles from the past week are considered valid.

6. **Concurrency Management**:
   - `SemaphoreSlim` limits the number of concurrent article processing tasks, based on `BATCH_ARTICLE`.

7. **Data Storage**:
   - Valid articles are appended to a JSON file, ensuring no duplicates.
   - Top 10 articles based on likes are recalculated and saved.

8. **Controller Operations**:
   - **Execute Endpoint**:
     - Endpoint: `POST /webcrawler/v1/execute`
     - Initiates the crawling process by calling `CrawlerHandler.StartCrawlerAsync`.
     - Manages concurrent execution to prevent multiple crawls of the same website.
   - **Load Endpoint**:
     - Endpoint: `POST /webcrawler/v1/load`
     - Calls `LoadHandler.LoadData` to retrieve processed data.

#### Error Handling

Errors during fetching and processing are logged, and retries are implemented with delays. Invalid URLs and parsing errors are handled gracefully, ensuring the crawler continues processing remaining items.

### Limitations

This Simple Web Crawler has a few limitations:
- Test coverage: 0%
- It cannot fetch menu or sub-menu links with pagination, missing older articles. A scheduler could handle this more efficiently with database storage instead of JSON files.
- Certain links (e.g., `https://vnexpress.net/podcast/vnexpress-hom-nay` and `https://vnexpress.net/the-thao/hau-truong`) cannot be fetched due to PuppeteerSharp limitations. A blacklist can handle these URLs.
- The current design does not efficiently handle concurrent processing of multiple websites or separate menu/sub-menu processing into worker pools.
- Crawling duration is around 60-70 minutes per 8 CPUs in a Docker container environment.
- Timeouts and retries need to be revisited

## How to Run

**Prerequisites**

- Docker installed on your local machine. Download Docker from the [official website](https://www.docker.com/products/docker-desktop/).

To run the project locally:

```bash
docker-compose up --build -d
```

Access the simple UI at `localhost:80` to execute or load data.

To view the crawler process logs:

```bash
docker-compose logs -f backend
```

This setup ensures a smooth deployment and operation of the Simple Web Crawler, enabling efficient article fetching and processing from supported websites.
