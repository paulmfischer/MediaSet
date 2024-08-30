## Docker

### Building
Run the following command from the `MediaSet.Api` folder: `docker build -t mediaset-api .`

### Running
Run the following command from the `MediaSet.Api` folder: `docker run -it --rm -p 1085:8080 --name mediaset mediaset-api -e "MediaSetDatabase:ConnectionString=mongodb://cortext.local:27017" -e "MediaSetDatabase:DatabaseName=MediaSet" -e "MediaSetDatabase:BooksCollectionName=Books"`