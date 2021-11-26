if [ ! "$(docker ps -q -f name=notificationsdb)" ]; then
    if [ "$(docker ps -aq -f status=exited -f name=notificationsdb)" ]; then
        docker rm notificationsdb
    fi
    docker run --rm --name notificationsdb -e POSTGRES_PASSWORD=testpassword -p 127.0.0.1:5432:5432 -d postgres:11
    sleep 5
fi

SCRIPT_DIR="$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
psql "host=localhost port=5432 dbname=postgres user=postgres password=testpassword" -f ${SCRIPT_DIR}/init.sql